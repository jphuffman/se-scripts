/* 
/// Whip's Mouse-Aimed Rotor Turret Script v15 - 6/17/17    
*/ 
 
//============================================================= 
//You can change these variables if you really want to.  
//You do not need to if you just want to use the vanilla script. 
//============================================================= 
 
string groupNameTag = "MART"; //name tag of turret groups 
string elevationRotorNameTag = "Elevation"; //name of elevation (vertical) rotor for specific turret 
string azimuthRotorNameTag = "Azimuth"; //name of azimuth (horiontal) rotor for specific turret 
 
double mouseSpeedModifier = 0.25; //scales mouse input by this factor 
 
//If the mouse controls should be relative to the cockpit's orientation 
bool cockpitRelativeMouseControl = false; 
bool fireWeaponsOnCrouch = true; 
 
 
//////////////////////////////////////////////////// 
//================================================= 
//No touchey anything below here 
//================================================= 
//////////////////////////////////////////////////// 
const double updatesPerSecond = 60; 
 
const double timeMax = 1 / updatesPerSecond; 
 
double timeElapsed = 0; 
 
IMyMotorStator elevationRotor; 
List<IMyMotorStator> additionalElevationRotors = new List<IMyMotorStator>(); 
IMyMotorStator azimuthRotor; 
IMyShipController shipController; 
 
List<IMyTerminalBlock> weaponsAndTools = new List<IMyTerminalBlock>(); //need to clear this 
List<IMyTerminalBlock> additionalWeaponsAndTools = new List<IMyTerminalBlock>(); 
 
const double rad2deg = 180 / Math.PI; 
 
const double refreshInterval = 5; 
double timeSinceRefresh = 141; 
bool hasTurrets = false; 
 
Program() 
{ 
    BuildConfig(Me); 
} 
 
void Main(string arg) 
{ 
    try 
    { 
        if (arg.ToLower().Trim() == "setup") 
        { 
            UpdateConfig(Me); 
            hasTurrets = GrabBlockGroups(); 
            timeSinceRefresh = 0; 
        } 
 
        timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds; 
        timeSinceRefresh += Runtime.TimeSinceLastRun.TotalSeconds; 
 
        if (timeElapsed >= timeMax) 
        { 
            Echo("WMI Mouse-Aimed Turret\nControl Systems\nOnline... " + RunningSymbol()); 
 
            if (!hasTurrets || timeSinceRefresh >= refreshInterval) //check if we are bot setup or if we have hit our refresh interval 
            { 
                hasTurrets = GrabBlockGroups(); 
                timeSinceRefresh = 0; 
            } 
 
            if (!hasTurrets) //if setup has failed 
                return; 
                 
            Echo($"\nNext block refresh in {Math.Round(Math.Max(0, refreshInterval - timeSinceRefresh))} seconds"); 
 
            foreach (IMyBlockGroup thisGroup in turretGroups) 
            { 
                Echo($"________________________\nGroup: '{thisGroup.Name}'"); 
 
                bool setupError = GrabBlocks(thisGroup); //grabs needed blocks in turret group 
 
                if (setupError) 
                { 
                    StopRotorMovement(thisGroup); //stops rotors from spazzing 
                    WeaponControl(false); //turns off any guns 
                } 
                else 
                { 
                    //control rotors 
                    RotorControl(shipController, thisGroup); 
                    Echo("Turret is online"); 
                } 
            } 
 
            //reset time count 
            timeElapsed = 0; 
        } 
    } 
    catch 
    { 
        Echo("Something broke yo"); 
        hasTurrets = false; 
    } 
} 
 
List<IMyBlockGroup> turretGroups = new List<IMyBlockGroup>(); 
 
bool GrabBlockGroups() 
{ 
    turretGroups.Clear(); 
    var groups = new List<IMyBlockGroup>(); 
 
    GridTerminalSystem.GetBlockGroups(groups); 
 
    foreach (IMyBlockGroup thisGroup in groups) 
    { 
        if (thisGroup.Name.ToLower().Contains(groupNameTag.ToLower())) 
        { 
            turretGroups.Add(thisGroup); 
        } 
    } 
 
    if (turretGroups.Count == 0) 
    { 
        Echo("Error: No MART groups found!"); 
        return false; 
    } 
    return true; 
} 
 
bool GrabBlocks(IMyBlockGroup thisGroup) 
{ 
    var blocks = new List<IMyTerminalBlock>(); 
    thisGroup.GetBlocks(blocks); 
 
    elevationRotor = null; 
    additionalElevationRotors.Clear(); 
    azimuthRotor = null; 
    shipController = null; 
    weaponsAndTools.Clear(); 
    additionalWeaponsAndTools.Clear(); 
 
    foreach (IMyTerminalBlock thisBlock in blocks) 
    { 
        if (thisBlock is IMyMotorStator) 
        { 
            if (thisBlock.CustomName.ToLower().Contains(elevationRotorNameTag.ToLower())) 
            { 
                if (elevationRotor == null) //grabs parent elevation rotor first 
                { 
                    var thisRotor = thisBlock as IMyMotorStator; 
                     
                    if (thisRotor.IsAttached && thisRotor.IsFunctional) //checks if elevation rotor is attached 
                    { 
                        thisGroup.GetBlocks(weaponsAndTools, block => block.CubeGrid == thisRotor.TopGrid && IsWeaponOrTool(block)); 
                    } 
                    if (weaponsAndTools.Count != 0) 
                        elevationRotor = thisRotor; 
                    else 
                         additionalElevationRotors.Add(thisRotor); 
                } 
                else //then grabs any other elevation rotors it finds 
                    additionalElevationRotors.Add(thisBlock as IMyMotorStator); 
            } 
            else if (thisBlock.CustomName.ToLower().Contains(azimuthRotorNameTag.ToLower())) //grabs azimuth rotor 
            { 
                azimuthRotor = thisBlock as IMyMotorStator; 
            } 
        } 
        else if (thisBlock is IMyShipController) //grabs ship controller 
        { 
            shipController = thisBlock as IMyShipController; 
        } 
    } 
 
    bool noErrors = true; 
    if (shipController == null) 
    { 
        Echo("Error: No control seat or remote control found"); 
        noErrors = false; 
    } 
 
    if (weaponsAndTools.Count == 0) 
    { 
        Echo("Error: No weapons or tools"); 
        noErrors = false; 
    } 
 
    if (azimuthRotor == null) 
    { 
        Echo("Error: No azimuth rotor"); 
        noErrors = false; 
    } 
 
    if (elevationRotor == null) 
    { 
        Echo("Error: No elevation rotor"); 
        noErrors = false; 
    } 
 
    if (additionalElevationRotors.Count == 0) 
    { 
        Echo("Optional: No opposite elevation rotors detected"); 
    } 
 
    return !noErrors; //ehhh this reads as "not no errors", kinda triggers me, but im too lazy to go flip all those booleans above :P 
} 
 
bool IsWeaponOrTool(IMyTerminalBlock block) 
{ 
    if (block is IMyUserControllableGun && !(block is IMyLargeTurretBase)) 
    { 
        return true; 
    } 
    else if (block is IMyShipToolBase) 
    { 
        return true; 
    } 
    else if (block is IMyLightingBlock) 
    { 
        return true; 
    } 
    else if (block is IMyCameraBlock) 
    { 
        return true; 
    } 
    else 
    { 
        return false; 
    } 
} 
 
void RotorControl(IMyShipController turretController, IMyBlockGroup thisGroup) 
{ 
    if (!turretController.IsUnderControl) 
    { 
        StopRotorMovement(thisGroup); 
        return; 
    } 
 
    //get orientation of turret 
    IMyTerminalBlock turretReference = weaponsAndTools[0]; 
    Vector3D turretFrontVec = turretReference.WorldMatrix.Forward; 
    Vector3D absUpVec = azimuthRotor.WorldMatrix.Up; 
    Vector3D turretSideVec = elevationRotor.WorldMatrix.Up; 
    Vector3D turretFrontCrossSide = turretFrontVec.Cross(turretSideVec); 
 
    //check elevation rotor orientation w.r.t. reference 
    double yawMult = 1; 
    double pitchMult = 1; 
 
    if (absUpVec.Dot(turretFrontCrossSide) < 0) 
    { 
        pitchMult = -1; 
    } 
 
    Vector3D WASDinputVec = turretController.MoveIndicator; 
    var mouseInput = turretController.RotationIndicator; 
 
    //converting mouse input to angular velocity (simple Proportional controller) 
    //rotors have their own inherent damping so Derivative term isnt all that important 
    double pitchSpeed = mouseSpeedModifier * mouseInput.Y * yawMult; 
    double elevationSpeed = mouseSpeedModifier * -mouseInput.X * pitchMult; 
     
    double adjustedPitchSpeed = pitchSpeed; 
    double adjustedElevationSpeed = elevationSpeed; 
    var controllerWorldMatrix = turretController.WorldMatrix; 
     
    if (cockpitRelativeMouseControl) 
    { 
        if (controllerWorldMatrix.Left.Dot(absUpVec) > 0.7071) 
        { 
            adjustedPitchSpeed = -elevationSpeed; 
            adjustedElevationSpeed = pitchSpeed; 
        } 
        else if (controllerWorldMatrix.Right.Dot(absUpVec) > 0.7071) 
        { 
            adjustedPitchSpeed = elevationSpeed; 
            adjustedElevationSpeed = -pitchSpeed; 
        } 
        else if (controllerWorldMatrix.Down.Dot(absUpVec) > 0.7071) 
        { 
            adjustedPitchSpeed = -pitchSpeed; 
            adjustedElevationSpeed = -elevationSpeed; 
        } 
    } 
 
    //apply rotor velocities 
    azimuthRotor.SetValue("Velocity", (float)adjustedPitchSpeed); 
    elevationRotor.SetValue("Velocity", (float)adjustedElevationSpeed); 
 
    //Determine how to move opposite elevation rotor (if any) 
    foreach(var additionalElevationRotor in additionalElevationRotors) 
    { 
 
        if (!additionalElevationRotor.IsAttached) //checks if opposite elevation rotor is attached 
        { 
            Echo($"Optional: No rotor head for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor..."); 
            continue; 
        } 
         
        thisGroup.GetBlocks(additionalWeaponsAndTools, block => block.CubeGrid == additionalElevationRotor.TopGrid && IsWeaponOrTool(block)); 
         
        if (additionalWeaponsAndTools.Count == 0) 
        { 
            Echo($"Optional: No weapons or tools for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor..."); 
            continue; 
        } 
        
        var oppositeFrontVec = additionalWeaponsAndTools[0].WorldMatrix.Forward; 
         
        float multiplier = -1f; 
        if (additionalElevationRotor.WorldMatrix.Up.Dot(elevationRotor.WorldMatrix.Up) > 0) 
            multiplier = 1f; 
 
        //flattens the opposite elevation rotor's forward vec onto the rotation plane of the parent elevation rotor 
        var oppositePlanar = oppositeFrontVec - VectorProjection(oppositeFrontVec, turretSideVec); 
 
        //Angular difference between elevation and additionalElevation rotor 
        var diff = (float)VectorAngleBetween(oppositePlanar, turretFrontVec) * Math.Sign(oppositePlanar.Dot(turretFrontCrossSide)) * 100;                                                                                                                                                                   //w/h-i+p!l_a#s$h%1^4&1 
 
        //Echo($"Error: {diff}"); 
 
        //Apply velocity while compensating for angular error 
        //This syncs the movement of both elevation rotors! 
        additionalElevationRotor.SetValue("Velocity", multiplier * (float)adjustedElevationSpeed - multiplier * diff); 
         
        if (fireWeaponsOnCrouch) 
        { 
            //control weapons 
            if (WASDinputVec.Y < 0) 
            { 
                ControlWeaponsAndTools(additionalWeaponsAndTools, true); 
            } 
            else 
            { 
                ControlWeaponsAndTools(additionalWeaponsAndTools, false); 
            } 
        } 
    } 
 
    //control weapons 
    if (fireWeaponsOnCrouch) 
    { 
        if (WASDinputVec.Y < 0) 
        { 
            ControlWeaponsAndTools(weaponsAndTools, true); 
        } 
        else 
        { 
            ControlWeaponsAndTools(weaponsAndTools, false); 
        } 
    } 
} 
 
Vector3D VectorProjection(Vector3D a, Vector3D b) 
{ 
    return a.Dot(b) / b.LengthSquared() * b; 
} 
 
double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians  
{ 
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) 
        return 0; 
    else 
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1)); 
} 
 
void WeaponControl(bool shouldEnable) 
{ 
    ControlWeaponsAndTools(weaponsAndTools, shouldEnable); 
} 
 
void ControlWeaponsAndTools(List<IMyTerminalBlock> weaponsAndTools, bool shouldEnable) 
{ 
    if (shouldEnable) 
    { 
        for (int i = 0; i < weaponsAndTools.Count; i++) 
        { 
            var weapon = weaponsAndTools[i] as IMyUserControllableGun; 
            weapon?.ApplyAction("Shoot_On"); 
            weapon?.ApplyAction("ShootOnce"); 
            var tool = weaponsAndTools[i] as IMyShipToolBase; 
            tool?.ApplyAction("OnOff_On"); 
            var light = weaponsAndTools[i] as IMyLightingBlock; 
            light?.ApplyAction("OnOff_On"); 
        } 
    } 
    else 
    { 
        for (int i = 0; i < weaponsAndTools.Count; i++) 
        { 
            var weapon = weaponsAndTools[i] as IMyUserControllableGun; 
            weapon?.ApplyAction("Shoot_Off"); 
            var tool = weaponsAndTools[i] as IMyShipToolBase; 
            tool?.ApplyAction("OnOff_Off"); 
            var light = weaponsAndTools[i] as IMyLightingBlock; 
            light?.ApplyAction("OnOff_Off"); 
        } 
    } 
} 
 
void StopRotorMovement(IMyBlockGroup thisGroup) 
{ 
    azimuthRotor?.SetValue("Velocity", 0f); 
    elevationRotor?.SetValue("Velocity", 0f); 
 
    foreach (var additionalElevationRotor in additionalElevationRotors) 
        additionalElevationRotor?.SetValue("Velocity", 0f); 
         
    var blocks = new List<IMyTerminalBlock>(); 
    thisGroup.GetBlocks(blocks, IsWeaponOrTool); 
    ControlWeaponsAndTools(blocks, false); 
} 
 
//Whip's Running Symbol Method v7 
int runningSymbolVariant = 0; 
string RunningSymbol() 
{ 
    runningSymbolVariant++; 
    string strRunningSymbol = ""; 
 
    if (runningSymbolVariant < 10) 
        strRunningSymbol = "|"; 
    else if (runningSymbolVariant < 20) 
        strRunningSymbol = "/"; 
    else if (runningSymbolVariant < 30) 
        strRunningSymbol = "--"; 
    else if (runningSymbolVariant < 40) 
        strRunningSymbol = "\\"; 
    else 
    { 
        strRunningSymbol = "|"; 
        runningSymbolVariant = -1; 
    } 
 
    return strRunningSymbol; 
} 
 
 
//Whips Variable Configuration methods v2 - 6/9/17 
#region VARIABLE CONFIG 
Dictionary<string, string> configDict = new Dictionary<string, string>(); 
 
void BuildConfig(IMyTerminalBlock block) 
{ 
    configDict.Clear(); 
    configDict.Add("groupNameTag", groupNameTag.ToString()); 
    configDict.Add("elevationRotorNameTag", elevationRotorNameTag.ToString()); 
    configDict.Add("azimuthRotorNameTag", azimuthRotorNameTag.ToString()); 
    configDict.Add("mouseSpeedModifier", mouseSpeedModifier.ToString()); 
    configDict.Add("cockpitRelativeMouseControl", cockpitRelativeMouseControl.ToString()); 
    configDict.Add("fireWeaponsOnCrouch", fireWeaponsOnCrouch.ToString()); 
 
    UpdateConfig(block, true); 
} 
 
void UpdateConfig(IMyTerminalBlock block, bool isBuilding = false) 
{ 
    string customData = block.CustomData; 
    var lines = customData.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries); 
 
    foreach (var thisLine in lines) 
    { 
        var words = thisLine.Split('='); 
        if (words.Length == 2) 
        { 
            var variableName = words[0].Trim(); 
            var variableValue = words[1].Trim(); 
            string dictValue; 
            if (configDict.TryGetValue(variableName, out dictValue)) 
            { 
                configDict[variableName] = variableValue; 
            } 
        } 
    } 
 
    GetVariableFromConfig("groupNameTag", ref groupNameTag); 
    GetVariableFromConfig("elevationRotorNameTag", ref elevationRotorNameTag); 
    GetVariableFromConfig("azimuthRotorNameTag", ref azimuthRotorNameTag); 
    GetVariableFromConfig("mouseSpeedModifier", ref mouseSpeedModifier); 
    GetVariableFromConfig("cockpitRelativeMouseControl", ref cockpitRelativeMouseControl); 
    GetVariableFromConfig("fireWeaponsOnCrouch", ref fireWeaponsOnCrouch); 
 
    WriteConfig(block); 
 
    if (isBuilding) 
        Echo("Config Loaded"); 
    else 
        Echo("Config Updated"); 
} 
 
StringBuilder configSB = new StringBuilder(); 
void WriteConfig(IMyTerminalBlock block) 
{ 
    configSB.Clear(); 
    foreach (var keyValue in configDict) 
    { 
        configSB.AppendLine($"{keyValue.Key} = {keyValue.Value}"); 
    } 
 
    block.CustomData = configSB.ToString(); 
} 
 
void GetVariableFromConfig(string name, ref bool variableToUpdate) 
{ 
    string valueStr; 
    if (configDict.TryGetValue(name, out valueStr)) 
    { 
        bool thisValue; 
        if (bool.TryParse(valueStr, out thisValue)) 
        { 
            variableToUpdate = thisValue; 
        } 
    } 
} 
 
void GetVariableFromConfig(string name, ref int variableToUpdate) 
{ 
    string valueStr; 
    if (configDict.TryGetValue(name, out valueStr)) 
    { 
        int thisValue; 
        if (int.TryParse(valueStr, out thisValue)) 
        { 
            variableToUpdate = thisValue; 
        } 
    } 
} 
 
void GetVariableFromConfig(string name, ref float variableToUpdate) 
{ 
    string valueStr; 
    if (configDict.TryGetValue(name, out valueStr)) 
    { 
        float thisValue; 
        if (float.TryParse(valueStr, out thisValue)) 
        { 
            variableToUpdate = thisValue; 
        } 
    } 
} 
 
void GetVariableFromConfig(string name, ref double variableToUpdate) 
{ 
    string valueStr; 
    if (configDict.TryGetValue(name, out valueStr)) 
    { 
        double thisValue; 
        if (double.TryParse(valueStr, out thisValue)) 
        { 
            variableToUpdate = thisValue; 
        } 
    } 
} 
 
void GetVariableFromConfig(string name, ref string variableToUpdate) 
{ 
    string valueStr; 
    if (configDict.TryGetValue(name, out valueStr)) 
    { 
        variableToUpdate = valueStr; 
    } 
} 
#endregion 
 
/* 
/// WHAT'S CHANGED /// 
v9 
* Optimized block group searching to only occur every 5 seconds. 
v10 
* Added support for as many elevation rotors as you want per turret 
Version 11 
* Fixed algorigthm for picking the correct parent elevation rotor 
v12 
* Fixed weapons not firing on command on when you had more than 2 elevation rotors 
v13 
* Added option to control turrets relative to the controlling cockpit 
v14 
* Added cockpitRelativeMouseControl variable to customData config 
v15 
* Added option to disable firing with [crouch] key 
*/