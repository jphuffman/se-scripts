/*
/// Whip's Turret Slaver v63 /// - revision: 3/19/18 
--------------------------------------------------------------------------------
================README==================
--------------------------------------------------------------------------------
It is recommended that you read all the instructions before attempting 
to use this code! This will make troubleshooting any issues much easier <3

----------------------------------------------------------------
/// Script Setup ///
----------------------------------------------------------------
    1) Place this script in a program block
    2) Make a timer with the following actions:
        - "Start" itself
        - "Trigger Now itself
        - "Run with default argument" this program
    3) Start the timer
    4) Set up turret groups (see below sections)

    DON'T FORGET TO SET YOUR ROTOR LIMITS!

    (Optional): You can adjust the variables at the top of the code
    if you dislike my default settings. I've found these values to
    be sufficient for vanilla weapons :)

----------------------------------------------------------------
/// Turret Group Names ///
----------------------------------------------------------------
    Turret groups must be named like the following:

        "Turret Group <ID>"

    Where <ID> is the unique identification tag of the turret.

    Example Turret Group Names:
        - Turret Group 1
        - Turret Group WhiplashIsAwesome
        - Turret Group SamIsACow
        - Turret Group 1A

----------------------------------------------------------------
/// Turret Group Components ///
----------------------------------------------------------------
    EACH turret group must have:
    - One designator turret with "Designator" in its name
    - One azimuthal (horizontal) rotor with "Azimuth" in its name
    - One or Two elevation (vertical) rotor(s) with "Elevation" in its name
    - At least one weapon or tool (any name you desire)
        can be a rocket launcher, gatling gun, camera, welder, grinder, or spotlight 

    (Names don't matter beyond what is required)

----------------------------------------------------------------
/// Code Arguments (Optional) ///
----------------------------------------------------------------
    Run the program ONCE with the following arguments if you desire

    reset_targeting : Resets targeting of all non-designator turrets


----------------------------------------------------------------
/// Whip's Notes ///
----------------------------------------------------------------
Post any questions, suggestions, or issues you have on the workshop page :D

Code by Whiplash141
*/

//=============================================================
//You can change these variables if you really want to.
//You do not need to if you just want to use the vanilla script.
//=============================================================

//Base name tag of turret groups
const string rotorTurretGroupNameTag = "Turret Group";
const string aiTurretGroupNameTag = "Slaved Group";

//These are the required block name tags in a turret group
const string elevationRotorName = "Elevation"; //name of elevation (vertical) rotor for specific turret
const string azimuthRotorName = "Azimuth"; //name of azimuth (horizontal) rotor for specific turret
const string designatorName = "Designator"; //name of the designator turret for specific group

//Angle that the turret will fire on if target is within this angle from the front of it
const double toleranceAngle = 5;

//Velocity of the projectiles that this code will fire
const double defaultProjectileSpeed = 400;

//Controls the speed of rotation; you probably shouldn't touch this
const double equilibriumRotationSpeed = 10;
const double proportionalGain = 75;
const double intergalGain = 25;
const double derivativeGain = 0;

//this is the distance that the turret will focus on IFF manually controlling the turret
double convergenceRange = 400;

////////////////////////////////////////////////////
//=================================================
//No touchey anything below here
//=================================================
////////////////////////////////////////////////////

double timeElapsed = 141;
const double rad2deg = 180.0 / Math.PI;
const double deg2rad = Math.PI / 180.0;
const double updatesPerSecond = 10;
const double timeMax = 1 / updatesPerSecond;
const double refreshTime = 10; //seconds
const double secondsPerRun = 1.0 / 60.0;
double currentTime = 141;
bool isSetup = false;
bool useVelocityEstimation = true;

Vector3D lastGridPosition = Vector3D.Zero;
Vector3D gridVelocity = Vector3D.Zero;
IMyShipController reference = null;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Once;
}

void Main(string arg, UpdateType updateType)
{
    //Bandaid because of ken sofwar
    if ((updateType & UpdateType.Once) != 0)
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    if ((updateType & (UpdateType.Script | UpdateType.Terminal) | UpdateType.Trigger) != 0)
        ArgumentHandling(arg);

    if ((updateType & UpdateType.Update1) == 0)
        return;

    //Update time count
    timeElapsed += secondsPerRun;
    currentTime += secondsPerRun;

    //Check for block setup
    if (!isSetup || currentTime >= refreshTime)
    {
        GetBlockGroups();
        GetVelocityReference();
        currentTime = 0;
        isSetup = true;
    }

    //Update grid position
    if (useVelocityEstimation)
    {
        var currentGridPosition = Me.CubeGrid.WorldAABB.Center; //get grid's bounding box center, decent approximation for CoM
        gridVelocity = (currentGridPosition - lastGridPosition) * 60.0;
        lastGridPosition = currentGridPosition;
    }
    else
    {
        if (DoesBlockExist(reference))
        {
            gridVelocity = reference.GetShipVelocities().LinearVelocity;
        }
        else
        {
            GetVelocityReference();
        }
    }

    //Run brunt of logic
    if (timeElapsed < timeMax)
        return;

    Echo("WMI Turret Control\nSystems Online... " + RunningSymbol());
    Echo($"\nNext block refresh in {Math.Max(0, refreshTime - currentTime):N0} second(s)\n");

    Echo($"Precise velocity: {!useVelocityEstimation}\n");

    //Verbose output
    if (rotorTurretsCount == 0)
    {
        Echo("> No rotor turret groups found");
    }
    else
    {
        Echo($"> {rotorTurretsCount} rotor turret group(s) found");
    }

    if (AITurretsCount == 0)
    {
        Echo("> No slaved AI turret groups found\n");
    }
    else
    {
        Echo($"> {AITurretsCount} slaved AI turret group(s) found\n");
    }

    if (rotorTurretsCount == 0 && AITurretsCount == 0)
    {
        return;
    }

    try
    {
        //Turret control
        foreach (var turret in turretList)
        {
            turret.DoWork(gridVelocity);
        }
    }
    catch
    {
        Echo("CRITICAL ERROR\nREFRESHING...");
        turretList.Clear();
        isSetup = false;
    }

    //reset time count
    timeElapsed = 0;
}

List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
List<IMyBlockGroup> allCurrentGroups = new List<IMyBlockGroup>();
List<IMyBlockGroup> currentRotorTurretGroups = new List<IMyBlockGroup>();
List<IMyBlockGroup> currentAITurretGroups = new List<IMyBlockGroup>();
List<TurretGroup> turretList = new List<TurretGroup>();
List<IMyShipController> shipControllers = new List<IMyShipController>();
int rotorTurretsCount = 0, AITurretsCount = 0;

void GetBlockGroups()
{
    GridTerminalSystem.GetBlockGroups(groups);
    currentAITurretGroups.Clear();
    currentRotorTurretGroups.Clear();
    allCurrentGroups.Clear();

    foreach (IMyBlockGroup thisGroup in groups)
    {
        if (StringExtensions.Contains(thisGroup.Name, aiTurretGroupNameTag, StringComparison.OrdinalIgnoreCase))
        {
            currentAITurretGroups.Add(thisGroup);
            allCurrentGroups.Add(thisGroup);
        }
        else if (StringExtensions.Contains(thisGroup.Name, rotorTurretGroupNameTag, StringComparison.OrdinalIgnoreCase))
        {
            currentRotorTurretGroups.Add(thisGroup);
            allCurrentGroups.Add(thisGroup);
        }
    }

    //Remove non-existing turrets from master list
    turretList.RemoveAll(x => !allCurrentGroups.Contains(x.ThisGroup));

    //Save our counts
    rotorTurretsCount = currentRotorTurretGroups.Count;
    AITurretsCount = currentAITurretGroups.Count;

    //Update existing turrets
    foreach (var turret in turretList)
    {
        turret.GetTurretGroupBlocks();

        //Remove existing turrets from list
        if (turret.IsRotorTurret)
        {
            currentRotorTurretGroups.Remove(turret.ThisGroup);
        }
        else
        {
            currentAITurretGroups.Remove(turret.ThisGroup);
        }
    }

    //Add new turret groups to the master list
    foreach (var group in currentAITurretGroups)
    {
        var turret = new TurretGroup(group, convergenceRange, designatorName, this, false);
        turretList.Add(turret);
    }

    foreach (var group in currentRotorTurretGroups)
    {
        var turret = new TurretGroup(group, convergenceRange, designatorName, this, true);
        turretList.Add(turret);
    }
}

void GetVelocityReference()
{
    reference = GetFirstBlockOfType<IMyShipController>();
    useVelocityEstimation = reference == null;
}

bool DoesBlockExist(IMyTerminalBlock block)
{
    return block.CubeGrid?.GetCubeBlock(block.Position)?.FatBlock == block;
}

T GetFirstBlockOfType<T>(string filterName = "") where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();

    if (filterName == "")
        GridTerminalSystem.GetBlocksOfType(blocks);
    else
        GridTerminalSystem.GetBlocksOfType(blocks, x => x.CustomName.Contains(filterName));

    return blocks.Count > 0 ? blocks[0] : null;
}

class TurretGroup
{
    IMyMotorStator elevationRotor;
    IMyMotorStator azimuthRotor;
    IMyLargeTurretBase designator;

    List<IMyMotorStator> additionalElevationRotors = new List<IMyMotorStator>();
    List<IMyTerminalBlock> allWeaponsAndTools = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock> primaryWeaponsAndTools = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock> additionalWeaponsAndTools = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock> groupBlocks = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock> slavedTurrets = new List<IMyTerminalBlock>();
    List<IMyLargeTurretBase> allDesignators = new List<IMyLargeTurretBase>();
    List<IMyLargeTurretBase> targetingDesignators = new List<IMyLargeTurretBase>();

    PID elevationPID = new PID(proportionalGain, intergalGain, derivativeGain, .1, timeMax);
    PID azimuthPID = new PID(proportionalGain, intergalGain, derivativeGain, .1, timeMax);

    Program _program;
    public IMyBlockGroup ThisGroup { get; private set; }
    bool _isRotorTurret;
    bool _isSetup;
    double convergenceRange;
    double toleranceDotProduct = Math.Cos(toleranceAngle * Math.PI / 180);
    Vector3D gridVelocity;
    string designatorName;

    public bool IsRotorTurret
    {
        get { return _isRotorTurret; }
    }

    public TurretGroup(IMyBlockGroup group, double defaultConvergenceRange, string designatorName, Program program, bool isRotorTurret)
    {
        this.ThisGroup = group;
        this._program = program;
        this._isRotorTurret = isRotorTurret;
        this.convergenceRange = defaultConvergenceRange;
        this.designatorName = designatorName;

        GetTurretGroupBlocks();
    }

    public void DoWork(Vector3D gridVelocity)
    {
        Echo($"------------------------\nGroup: '{ThisGroup.Name}'");

        if (!_isSetup)
        {
            GetTurretGroupBlocks(true);

            if (_isRotorTurret)
                StopRotorMovement();

            return;
        }

        this.gridVelocity = gridVelocity;

        if (_isRotorTurret)
        {
            designator = GetClosestTargetingTurret(designatorName, allDesignators, azimuthRotor.GetPosition());

            if (designator == null) //second null check (if STILL null)
            {
                Echo($"Error: No designator turret found for group '{ThisGroup.Name}'");
                ShootWeapons(allWeaponsAndTools, false);
                return;
            }

            //guide on target
            if (designator.IsUnderControl || designator.HasTarget)
            {
                RotorControl(ThisGroup);
                Echo($"Rotor turret is targeting");
            }
            else
            {
                StopRotorMovement();
                ShootWeapons(allWeaponsAndTools, false);
                ReturnToEquilibrium();
                Echo($"Rotor turret is idle");
            }

            var num = elevationRotor == null ? 0 : 1;
            Echo($"Elevation rotors: {additionalElevationRotors.Count + num}");
            Echo($"Weapon count: {allWeaponsAndTools.Count}");
            Echo($"Designators: {allDesignators.Count}");
            Echo($" > Targeting: {targetingDesignators.Count}");

        }
        else
        {
            designator = GetClosestTargetingTurret(designatorName, allDesignators, GetAverageWeaponPosition(slavedTurrets));

            if (designator == null) //second null check (if STILL null)
            {
                Echo($"Error: No designator turret found for group '{ThisGroup.Name}'");
                ShootWeapons(allWeaponsAndTools, false);
                return;
            }

            //guide on target
            if (designator.IsUnderControl || designator.HasTarget)
            {
                SlavedTurretControl();
                Echo($"Slaved turret(s) are targeting");
            }
            else
            {
                ShootWeapons(slavedTurrets, false); //force shooting off
                Echo($"Slaved turret(s) are idle");
            }

            Echo($"Slaved turret count: {slavedTurrets.Count}");
            Echo($"Designators: {allDesignators.Count}");
            Echo($" > Targeting: {targetingDesignators.Count}");
        }
    }

    void Echo(string data)
    {
        _program.Echo(data);
    }

    #region Grabbing Blocks
    public void GetTurretGroupBlocks(bool verbose = false)
    {
        ThisGroup.GetBlocks(groupBlocks);

        if (_isRotorTurret)
            _isSetup = GrabBlocks(groupBlocks, verbose);
        else
            _isSetup = GrabBlocksAI(groupBlocks, verbose);
    }

    bool GrabBlocks(List<IMyTerminalBlock> groupBlocks, bool verbose)
    {
        elevationRotor = null;
        additionalElevationRotors.Clear();
        azimuthRotor = null;
        designator = null;
        allWeaponsAndTools.Clear();
        primaryWeaponsAndTools.Clear();
        additionalWeaponsAndTools.Clear();
        allDesignators.Clear();

        ThisGroup.GetBlocks(groupBlocks);

        foreach (IMyTerminalBlock thisBlock in groupBlocks)
        {
            if (IsWeaponOrTool(thisBlock))
                allWeaponsAndTools.Add(thisBlock);

            if (thisBlock is IMyMotorStator)
            {
                if (thisBlock.CustomName.ToLower().Contains(elevationRotorName.ToLower()))
                {
                    if (elevationRotor == null) //grabs parent elevation rotor first
                    {
                        var thisRotor = thisBlock as IMyMotorStator;

                        if (thisRotor.IsAttached && thisRotor.IsFunctional) //checks if elevation rotor is attached
                        {
                            ThisGroup.GetBlocks(primaryWeaponsAndTools, block => block.CubeGrid == thisRotor.TopGrid && IsWeaponOrTool(block));
                        }
                        if (primaryWeaponsAndTools.Count != 0)
                            elevationRotor = thisRotor;
                        else
                            additionalElevationRotors.Add(thisRotor);
                    }
                    else //then grabs any other elevation rotors it finds
                        additionalElevationRotors.Add(thisBlock as IMyMotorStator);
                }
                else if (thisBlock.CustomName.ToLower().Contains(azimuthRotorName.ToLower()))
                {
                    azimuthRotor = thisBlock as IMyMotorStator;
                }
            }
            else if (thisBlock is IMyLargeTurretBase && thisBlock.CustomName.ToLower().Contains(designatorName.ToLower())) //grabs ship controller
            {
                //designator = thisBlock as IMyLargeTurretBase;
                allDesignators.Add(thisBlock as IMyLargeTurretBase);
            }
        }

        if (elevationRotor != null && elevationRotor.IsAttached) //grabs weapons on elevation turret's rotor head grid
        {
            ThisGroup.GetBlocks(primaryWeaponsAndTools, block => block.CubeGrid == elevationRotor.TopGrid && IsWeaponOrTool(block));
        }

        bool noErrors = true;

        /*if (designator == null && azimuthRotor != null) //first null check for designator
        {
            //grabs closest designator to the turret base
            designator = GetClosestTargetingTurret(designatorName, allDesignators, azimuthRotor.GetPosition());
        }*/

        /*
        if (designator == null) //second null check (if STILL null)
        {
            Echo($"Error: No designator turret found for group '{ThisGroup.Name}'");
            noErrors = false;
        }*/

        if (primaryWeaponsAndTools.Count == 0)
        {
            if (verbose)
                Echo("Error: No weapons or tools");
            noErrors = false;
        }

        if (azimuthRotor == null)
        {
            if (verbose)
                Echo("Error: No azimuth rotor");
            noErrors = false;
        }

        if (elevationRotor == null)
        {
            if (verbose)
                Echo("Error: No elevation rotor");
            noErrors = false;
        }

        return noErrors;
    }

    bool GrabBlocksAI(List<IMyTerminalBlock> groupBlocks, bool verbose)
    {
        designator = null;
        slavedTurrets.Clear();
        allDesignators.Clear();

        foreach (IMyTerminalBlock thisBlock in groupBlocks)
        {
            var turret = thisBlock as IMyLargeTurretBase;
            if (turret == null)
                continue;

            if (turret.CustomName.ToLower().Contains(designatorName.ToLower()))
            {
                allDesignators.Add(turret);
            }
            else
            {
                turret.SetValue("Range", 1f);
                if (turret.EnableIdleRotation)
                    turret.EnableIdleRotation = false;
                slavedTurrets.Add(turret);
            }

        }

        bool setupError = false;
        if (slavedTurrets.Count == 0)
        {
            if (verbose)
                Echo($"Error: No slaved AI turrets found");
            setupError = true;
        }

        if (allDesignators.Count == 0) //second null check (If STILL null)
        {
            if (verbose)
                Echo($"Error: No designator turrets found");
            setupError = true;
        }

        return !setupError;
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
    #endregion

    #region Targeting Functions
    Vector3D GetTargetPoint(Vector3D shooterPosition, IMyLargeTurretBase designator, bool isRocket = false, double projectileVelocity = defaultProjectileSpeed)
    {
        //get designator position
        Vector3D designatorPos = designator.GetPosition();

        //get vector where designator is pointing
        double designatorAzimuth = designator.Azimuth;
        double designatorElevation = designator.Elevation;

        Vector3D targetVec = Vector3D.Zero;
        if (designator.IsUnderControl)
        {
            targetVec = designatorPos + VectorAzimuthElevation(designator) * convergenceRange;
        }
        else if (designator.HasTarget) //if designator has target
        {
            var targetInfo = designator.GetTargetedEntity();

            if (isRocket)
                targetVec = CalculateProjectileInterceptMissile(projectileVelocity, gridVelocity, shooterPosition, targetInfo.Velocity, targetInfo.Position);
            else
                targetVec = CalculateProjectileIntercept(projectileVelocity, gridVelocity, shooterPosition, targetInfo.Velocity, targetInfo.Position);
        }
        return targetVec;
    }

    // Whip's CalculateProjectileIntercept method v2 - 8/2/17 //
    Vector3D CalculateProjectileIntercept(double projectileSpeed, Vector3D shooterVelocity, Vector3D shooterPosition, Vector3D targetVelocity, Vector3D targetPos)
    {
        var directHeading = (targetPos + timeMax * targetVelocity) - shooterPosition;
        var directHeadingNorm = Vector3D.Normalize(directHeading);

        var relativeVelocity = targetVelocity - shooterVelocity;

        var parallelVelocity = relativeVelocity.Dot(directHeadingNorm) * directHeadingNorm;
        var normalVelocity = relativeVelocity - parallelVelocity;

        var diff = projectileSpeed * projectileSpeed - normalVelocity.LengthSquared();
        if (diff < 0)
        {
            return targetPos;
        }

        return Math.Sqrt(diff) * directHeadingNorm + normalVelocity + shooterPosition;
    }

    Vector3D CalculateProjectileInterceptMissile(double projectileSpeed, Vector3D shooterVelocity, Vector3D shooterPosition, Vector3D targetVelocity, Vector3D targetPos)
    {
        var firstInterceptGuess = CalculateProjectileIntercept(projectileSpeed, Vector3D.Zero, shooterPosition, targetVelocity, targetPos);

        var forwardDirection = firstInterceptGuess - shooterPosition;

        var lateralShooterVelocity = VectorRejection(shooterVelocity, forwardDirection);
        var forwardShooterVelocity = shooterVelocity - lateralShooterVelocity;
        var lateralShooterSpeed = lateralShooterVelocity.Length();
        var forwardShooterSpeed = forwardShooterVelocity.Length();

        var displacement = CalculateMissileLateralDisplacement(forwardShooterSpeed, lateralShooterSpeed, Vector3D.DistanceSquared(shooterPosition, targetPos));// * .75;
        var displacementVec = lateralShooterSpeed == 0 ? Vector3D.Zero : -displacement * lateralShooterVelocity / lateralShooterSpeed;

        return firstInterceptGuess + displacementVec / 2;
    }

    //Whip's CalculateMissileLateralDisplacement Method v2 - 1/20/18
    //2.857363e-05 * vf * vf + -4.217643e-04 * vl * vl + -1.633352e-03 * vl * vf + -3.655535e-03 * vf + 5.304202e-01 * vl + -1.930280e-02
    //2.924762e-05 * vf * vf + -4.211911e-04 * vl * vl + -1.632803e-03 * vl * vf + -3.731753e-03 * vf + 5.309973e-01 * vl + -1.742650e-02
    double[] coeffs = new double[] { 9.778358e-06, -4.310497e-04, -1.654714e-03, -1.302092e-03, 5.325910e-01, -7.455133e-02 };
    
    double[] coeffs800 = new double[] {9.778358e-06, -4.310497e-04, -1.654714e-03, -1.302092e-03, 5.325910e-01, -7.455133e-02};
    double[] coeffs400 = new double[] {9.062520e-06, -4.299310e-04, -1.649096e-03, -1.223164e-03, 5.310376e-01, -7.572690e-02};
    double[] coeffs200 = new double[] {-3.623750e-06, -4.100702e-04, -1.549518e-03, 1.761673e-04, 5.034959e-01, -9.658125e-02};
    double[] coeffs100 = new double[] {-4.772952e-05, -3.383196e-04, -1.201931e-03, 5.083057e-03, 4.068578e-01, -1.706490e-01};
    double[] coeffs050 = new double[] {-1.067714e-04, -2.235772e-04, -7.271787e-04, 1.194058e-02, 2.714081e-01, -2.806343e-01};

    double CalculateMissileLateralDisplacement(double forwardVelocity, double lateralVelocity, double distToTarget = 640000)
    {
        double[] coeffs = new double[0];
        if (distToTarget > (600 * 600))
            coeffs = coeffs800;
        else if (distToTarget > (300 * 300))
            coeffs = coeffs400;
        else if (distToTarget > (150 * 150))
            coeffs = coeffs200;
        else if (distToTarget > (75 * 75))
            coeffs = coeffs100;
        else
            coeffs = coeffs050;
        
        //Curve fit model found using regression
        var num1 = coeffs[0] * forwardVelocity * forwardVelocity;
        var num2 = coeffs[1] * lateralVelocity * lateralVelocity;
        var num3 = coeffs[2] * lateralVelocity * forwardVelocity;
        var num4 = coeffs[3] * forwardVelocity;
        var num5 = coeffs[4] * lateralVelocity;
        var num6 = coeffs[5];
        return num1 + num2 + num3 + num4 + num5 + num6;
    }

    bool IsProjectileRocket(IMyTerminalBlock block)
    {
        return block is IMyLargeMissileTurret || block is IMySmallMissileLauncher;
    }

    double GetProjectileVelocity(IMyTerminalBlock block)
    {
        if (block is IMyLargeGatlingTurret || block is IMySmallGatlingGun)
            return 400;
        else if (block is IMyLargeInteriorTurret)
            return 300;
        else if (block is IMyLargeMissileTurret || block is IMySmallMissileLauncher)
            return 200;
        else
            return defaultProjectileSpeed;
    }

    Vector3D GetAverageWeaponPosition(List<IMyTerminalBlock> weapons)
    {
        Vector3D positionSum = Vector3D.Zero;

        if (weapons.Count == 0)
            return positionSum;

        foreach (var block in weapons)
        {
            positionSum += block.GetPosition();
        }

        return positionSum / weapons.Count;
    }
    #endregion

    #region Weapon Control
    void WeaponControl(double deviation, IMyLargeTurretBase designator, List<IMyTerminalBlock> weaponsAndTools)
    {
        if (designator.IsUnderControl && designator.IsShooting && deviation < (toleranceAngle * deg2rad))
            ShootWeapons(weaponsAndTools, true);
        else if (deviation < (toleranceAngle * deg2rad) && designator.HasTarget) //fires if in tolerance angle
            ShootWeapons(weaponsAndTools, true);
        else //doesnt fire if not in tolerance angle or designator isnt controlled
            ShootWeapons(weaponsAndTools, false);
    }

    void ShootWeapons(List<IMyTerminalBlock> weaponList, bool shouldFire)
    {
        if (shouldFire)
        {
            for (int i = 0; i < weaponList.Count; i++)
            {
                var weaponToShoot = weaponList[i] as IMyUserControllableGun;

                weaponToShoot?.ApplyAction("Shoot_On");
                //weaponToShoot?.ApplyAction("ShootOnce");
            }
        }
        else
        {
            for (int i = 0; i < weaponList.Count; i++)
            {
                var weaponToShoot = weaponList[i] as IMyUserControllableGun;

                weaponToShoot?.ApplyAction("Shoot_Off");
            }
        }
    }
    #endregion

    #region Designator Selection
    void GetTargetingTurrets(List<IMyLargeTurretBase> allDesignators)
    {
        targetingDesignators.Clear();
        foreach (var block in allDesignators)
        {
            if (block.HasTarget || block.IsUnderControl)
            {
                targetingDesignators.Add(block);
            }
        }
    }

    //Whip's Get Closest Targeted Turret v2 - 1/5/18
    IMyLargeTurretBase GetClosestTargetingTurret(string name, List<IMyLargeTurretBase> allDesignators, Vector3D referencePos)
    {
        GetTargetingTurrets(allDesignators);

        if (targetingDesignators.Count == 0)
        {
            return GetClosestBlockOfType(allDesignators, referencePos);
        }
        else
        {
            return GetClosestBlockOfType(targetingDesignators, referencePos);
        }
    }

    //Whip's Get Closest Block of Type Method v6 Overload 1 - 1/5/18
    T GetClosestBlockOfType<T>(List<T> allBlocks, Vector3D referencePos, string name = "") where T : class, IMyTerminalBlock
    {
        if (allBlocks.Count == 0)
        {
            return null;
        }

        var closestBlock = default(T);

        double shortestDistance = double.MaxValue;

        foreach (T thisBlock in allBlocks)
        {
            var thisDistance = Vector3D.DistanceSquared(referencePos, thisBlock.GetPosition());

            if (thisDistance + 1E-6 < shortestDistance) //added in epsilon
            {
                closestBlock = thisBlock;
                shortestDistance = thisDistance;
            }
            //otherwise move to next one
        }

        return closestBlock;
    }
    #endregion

    #region Rotor Turret Control
    void RotorControl(IMyBlockGroup ThisGroup)
    {
        //get orientation of reference
        IMyTerminalBlock turretReference = primaryWeaponsAndTools[0];

        Vector3D turretFrontVec = turretReference.WorldMatrix.Forward;
        Vector3D absUpVec = azimuthRotor.WorldMatrix.Up;
        Vector3D turretSideVec = elevationRotor.WorldMatrix.Up;
        Vector3D turretFrontCrossSide = turretFrontVec.Cross(turretSideVec);

        //check elevation rotor orientation w.r.t. reference
        Vector3D turretUpVec;
        Vector3D turretLeftVec;
        if (absUpVec.Dot(turretFrontCrossSide) >= 0)
        {
            turretUpVec = turretFrontCrossSide;
            turretLeftVec = turretSideVec;
        }
        else
        {
            turretUpVec = -1 * turretFrontCrossSide;
            turretLeftVec = -1 * turretSideVec;
        }

        var shooterPosition = GetAverageWeaponPosition(allWeaponsAndTools);
        var targetPointVec = GetTargetPoint(shooterPosition, designator, IsProjectileRocket(primaryWeaponsAndTools[0]), GetProjectileVelocity(primaryWeaponsAndTools[0]));

        //get vector to target point
        Vector3D referenceToTargetVec = targetPointVec - shooterPosition;

        var baseUp = absUpVec;
        var baseLeft = baseUp.Cross(turretFrontVec);
        var baseForward = baseLeft.Cross(baseUp);

        double desiredAzimuthAngle, desiredElevationAngle, currentAzimuthAngle, currentElevationAngle, azimuthAngle, elevationAngle;

        GetRotationAngles(referenceToTargetVec, baseForward, baseLeft, baseUp, out desiredAzimuthAngle, out desiredElevationAngle);
        GetRotationAngles(turretFrontVec, baseForward, baseLeft, baseUp, out currentAzimuthAngle, out currentElevationAngle);

        azimuthAngle = desiredAzimuthAngle - currentAzimuthAngle;
        elevationAngle = desiredElevationAngle - currentElevationAngle;

        if (absUpVec.Dot(turretFrontCrossSide) >= 0)
        {
            elevationAngle *= -1;
        }

        double azimuthSpeed = azimuthPID.Control(azimuthAngle);
        double elevationSpeed = elevationPID.Control(elevationAngle);

        //control rotors 
        azimuthRotor.TargetVelocityRPM = -(float)azimuthSpeed; //negative because we want to cancel the positive angle via our movements
        elevationRotor.TargetVelocityRPM = -(float)elevationSpeed;

        //calculate deviation angle
        double deviationAngle = VectorAngleBetween(turretFrontVec, referenceToTargetVec);
        WeaponControl(deviationAngle, designator, primaryWeaponsAndTools);

        //Check opposite elevation rotor
        if (additionalElevationRotors.Count != 0)
        {
            foreach (var additionalElevationRotor in additionalElevationRotors) //Determine how to move opposite elevation rotor (if any)
            {
                if (!additionalElevationRotor.IsAttached) //checks if opposite elevation rotor is attached
                {
                    Echo($"Warning: No rotor head for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor...");
                    continue;
                }

                ThisGroup.GetBlocks(additionalWeaponsAndTools, block => block.CubeGrid == additionalElevationRotor.TopGrid && IsWeaponOrTool(block));

                if (additionalWeaponsAndTools.Count == 0)
                {
                    Echo($"Warning: No weapons or tools for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor...");
                    continue;
                }

                var oppositeFrontVec = additionalWeaponsAndTools[0].WorldMatrix.Forward;

                float multiplier = Math.Sign(additionalElevationRotor.WorldMatrix.Up.Dot(elevationRotor.WorldMatrix.Up));

                var diff = (float)VectorAngleBetween(oppositeFrontVec, turretFrontVec) * Math.Sign(oppositeFrontVec.Dot(turretFrontCrossSide)) * 100;
                additionalElevationRotor.TargetVelocityRPM = (float)elevationSpeed - multiplier * diff;

                WeaponControl(deviationAngle, designator, additionalWeaponsAndTools); //use same deviation angle b/c im assuming that it will be close

                //Echo($"Rotor: {additionalElevationRotor.CustomName}\ndiff: {diff}\nAngle: {additionalElevationRotor.Angle * deg2rad}");
            }
        }
    }

    void ReturnToEquilibrium()
    {
        MoveRotorToEquilibrium(azimuthRotor);
        MoveRotorToEquilibrium(elevationRotor);

        foreach (var block in additionalElevationRotors)
        {
            MoveRotorToEquilibrium(block);
        }
    }

    void MoveRotorToEquilibrium(IMyMotorStator rotor)
    {
        if (rotor == null)
            return;

        double restAngle = 0;
        if (!string.IsNullOrEmpty(rotor.CustomData) && double.TryParse(rotor.CustomData, out restAngle))
        {
            var restAngleRad = MathHelper.ToRadians((float)restAngle);
            restAngleRad %= MathHelper.TwoPi;
            //MathHelper.LimitRadians(ref restAngleRad);

            var currentAngle = rotor.Angle;
            currentAngle %= MathHelper.TwoPi;
            //MathHelper.LimitRadians(ref currentAngle);

            var angularDeviation = (restAngleRad - currentAngle) % MathHelper.TwoPi;
            if (angularDeviation > MathHelper.Pi)
            {
                angularDeviation = MathHelper.TwoPi - angularDeviation;
            }
            //MathHelper.LimitRadiansPI(ref angularDeviation);

            rotor.TargetVelocityRPM = (float)Math.Round(angularDeviation * equilibriumRotationSpeed, 2);
        }
        else if (rotor.LowerLimitRad >= -MathHelper.TwoPi && rotor.UpperLimitRad <= MathHelper.TwoPi)
        {
            var avgAngle = (rotor.LowerLimitRad + rotor.UpperLimitRad) / 2;
            var targetVelocity = (avgAngle - rotor.Angle) * equilibriumRotationSpeed;
            rotor.TargetVelocityRPM = (float)Math.Round(targetVelocity, 2);
        }
        else
        {
            rotor.TargetVelocityRPM = 0f;
        }
    }

    void StopRotorMovement()
    {
        azimuthRotor?.SetValue("Velocity", 0f);
        elevationRotor?.SetValue("Velocity", 0f);

        foreach (var additionalElevationRotor in additionalElevationRotors)
        {
            additionalElevationRotor.TargetVelocityRPM = 0f;
        }

        for (int i = 0; i < allWeaponsAndTools.Count; i++)
        {
            var thisWeapon = allWeaponsAndTools[0] as IMyUserControllableGun;
            thisWeapon?.ApplyAction("Shoot_Off");
        }
    }
    #endregion

    #region Slaved Turret Control
    void SlavedTurretControl()
    {
        //control AI turrets (if any)
        //aim all slaved turrets at target point
        foreach (IMyLargeTurretBase thisTurret in slavedTurrets)
        {
            var targetPointVec = GetTargetPoint(thisTurret.GetPosition(), designator, IsProjectileRocket(thisTurret), GetProjectileVelocity(thisTurret));

			
            //This shit broke yo
            thisTurret.SetTarget(targetPointVec); //this shit broke yo           
            var turretMatrix = thisTurret.WorldMatrix;
            Vector3D turretDirection = VectorAzimuthElevation(thisTurret);
            var normalizedTargetDirection = Vector3D.Normalize(targetPointVec - turretMatrix.Translation);
			

            double azimuth = 0; double elevation = 0;
            GetRotationAngles(normalizedTargetDirection, turretMatrix.Forward, turretMatrix.Left, turretMatrix.Up, out azimuth, out elevation);
            thisTurret.Azimuth = (float)azimuth;
            thisTurret.Elevation = (float)elevation;
            
            SyncTurretAngles(thisTurret);

            if (turretDirection.Dot(normalizedTargetDirection) >= toleranceDotProduct)
            {
                if (designator.IsShooting || (designator.HasTarget && !designator.IsUnderControl))
                {
                    thisTurret.ApplyAction("Shoot_On");
                    //thisTurret.ApplyAction("ShootOnce"); //Had to add this or the guns wont shoot...
                }
                else
                    thisTurret.ApplyAction("Shoot_Off");
            }
            else
            {
                thisTurret.ApplyAction("Shoot_Off");
            }
        }
    }

    void SyncTurretAngles(IMyLargeTurretBase turret)
    {
        turret.SyncAzimuth();
        turret.SyncElevation();
        turret.SyncEnableIdleRotation();
    }
    #endregion

    #region General Functions
    //Whip's Vector from Elevation and Azimuth
    static Vector3D VectorAzimuthElevation(IMyLargeTurretBase designator)
    {
        double el = designator.Elevation;
        double az = designator.Azimuth;

        //CreateFromAzimuthAndElevation(az, el, out localTargetVector)

        el = el % (2 * Math.PI);
        az = az % (2 * Math.PI);

        if (az != Math.Abs(az))
        {
            az = 2 * Math.PI + az;
        }

        int x_mult = 1;

        if (az > Math.PI / 2 && az < Math.PI)
        {
            az = Math.PI - (az % Math.PI);
            x_mult = -1;
        }
        else if (az > Math.PI && az < Math.PI * 3 / 2)
        {
            az = 2 * Math.PI - (az % Math.PI);
            x_mult = -1;
        }

        double x; double y; double z;

        if (el == Math.PI / 2)
        {
            x = 0;
            y = 0;
            z = 1;
        }
        else if (az == Math.PI / 2)
        {
            x = 0;
            y = 1;
            z = y * Math.Tan(el);
        }
        else
        {
            x = 1 * x_mult;
            y = Math.Tan(az);
            double v_xy = Math.Sqrt(1 + y * y);
            z = v_xy * Math.Tan(el);
        }

        var worldMatrix = designator.WorldMatrix;
        return Vector3D.Normalize(worldMatrix.Forward * x + worldMatrix.Left * y + worldMatrix.Up * z);
        //return new Vector3D(x, y, z);
    }

    //Whip's Get Rotation Angles Method v6 - 8/28/17
    //Modified yaw sign
    static void GetRotationAngles(Vector3D v_target, Vector3D v_front, Vector3D v_left, Vector3D v_up, out double yaw, out double pitch)
    {
        //Dependencies: VectorProjection() | VectorAngleBetween()
        var projectTargetUp = VectorProjection(v_target, v_up);
        var projTargetFrontLeft = v_target - projectTargetUp;

        yaw = VectorAngleBetween(v_front, projTargetFrontLeft);
        pitch = VectorAngleBetween(v_target, projTargetFrontLeft);

        //---Make sure our pitch does not exceed 90 degrees
        if (pitch > MathHelper.PiOver2)
        {
            pitch -= MathHelper.PiOver2;
        }

        //---Check if yaw angle is left or right  
        yaw = Math.Sign(v_left.Dot(v_target)) * yaw;

        //---Check if pitch angle is up or down    
        pitch = Math.Sign(v_up.Dot(v_target)) * pitch;

        //---Check if target vector is pointing opposite the front vector
        if (Math.Abs(yaw) <= 1E-6 && v_target.Dot(v_front) < 0)
        {
            yaw = Math.PI;
        }
    }

    static Vector3D VectorProjection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a.Dot(b) / b.LengthSquared() * b;
    }

    static Vector3D VectorRejection(Vector3D a, Vector3D b) //reject a on b    
    {
        if (Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    static double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }
    #endregion
}

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

void ArgumentHandling(string arg)
{
    switch (arg.ToLower())
    {
        case "reset_targeting":
            ResetTurretTargeting();
            break;

        default:
            break;
    }
}

void ResetTurretTargeting()
{
    var allDesignators = new List<IMyLargeTurretBase>();
    GridTerminalSystem.GetBlocksOfType(allDesignators);

    foreach (IMyLargeTurretBase thisTurret in allDesignators)
    {
        thisTurret.ResetTargetingToDefault();
        thisTurret.ApplyAction("Shoot_Off");
        //thisTurret.EnableIdleRotation = true;
        thisTurret.SetValue("Range", float.MaxValue);
    }
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 3;
string[] runningSymbols = new string[] { "−", "\\", "|", "/" };

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}

//Whip's PID controller class v6 - 11/22/17
public class PID
{
    double _kP = 0;
    double _kI = 0;
    double _kD = 0;
    double _integralDecayRatio = 0;
    double _lowerBound = 0;
    double _upperBound = 0;
    double _timeStep = 0;
    double _inverseTimeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;
    bool _integralDecay = false;
    public double Value { get; private set; }

    public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        _integralDecay = false;
    }

    public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        _integralDecayRatio = integralDecayRatio;
        _integralDecay = true;
    }

    public double Control(double error)
    {
        //Compute derivative term
        var errorDerivative = (error - _lastError) * _inverseTimeStep;

        if (_firstRun)
        {
            errorDerivative = 0;
            _firstRun = false;
        }

        //Compute integral term
        if (!_integralDecay)
        {
            _errorSum += error * _timeStep;

            //Clamp integral term
            if (_errorSum > _upperBound)
                _errorSum = _upperBound;
            else if (_errorSum < _lowerBound)
                _errorSum = _lowerBound;
        }
        else
        {
            _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep;
        }

        //Store this error as last error
        _lastError = error;

        //Construct output
        this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
        return this.Value;
    }

    public double Control(double error, double timeStep)
    {
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        return Control(error);
    }

    public void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}

/*
Change Log:
- Clamped values to account for floating point errors - v31
- Fixed syntax error - v32
- Added AI turret slaving support - v33
- Redesigned targeting parameters - v33
- Added rotor turret equilibrium function - v34
- Cleaned, simplified, and removed some functions - v35
- Redesigned turret sweeping function - v35
- Reverted back to old turret sweeping function XD - v35
- Added in support for AI turret groups - v35
- Workaround for turret angle setting bug -DONE - v37-1
- Grabs Weapons/Tools based on grid id of rotor head - v37-3
- Works with 2 elevation rotors per turret group - v39
- Tweaked get rotation angle method - v39
- Fixed broke ass WorldMatricies. Thanks keen... - v40
- Optimized position getting by adding GetWorldPosition() method - v41
- Changed GetClosestBlock method to GetClosestTargetingTurret - v42
- Fixed turrets spinning when idle when no rotor limits were set - v43
- Adjusted range computation for GetClosestTargetingTurret to avoid musical turrets - v44
- Removed useage of GetWorldMatrix and GetWorldPosition since the bug that necessitated their use is gone - v45
- Removed lots of unused math and methods - v45
- Added support for infinite numbers of elevation rotors - v46
- Turrets will only fire automatically when designator has line of sight - v47
- Decreased equilibrium turn speed for safety reasons - v47
- Designators can now rotate idly if the user so desires - v47
- Replaced GetVectorAzimuthElevation with GetRotationAngles - v48
- Added in a bunch of goodies from the ModAPI update - v49
- Added in runtime lead compensation - v49-1
- Fixed get closest targeting turret method - v49-2
- Cached designator lists for better performance - v49-3
- Added optional equilibrium position input for rotors - v49-4
- Changed some math with the equilibrium angle - v51
- Fixed rocket lead! - v52
- Fixed issue where leading was not being determined by the correct weapon/tool list - v53
- Added new running symbol code - v53
- Fixed rocket lead breaking when stationary - v54
- Restructured entire script to be more performant - v55
- Moved everything into classes - v55
- Added a block refresh interval - v55
- Added PID control to the rotor turret groups - v56
- Fixed typo of equilibrium all throughout the code XD - v57
- Fixed designator turret not being recognized for slaved groups - v58
- Changed block grabbing return function to fix exception issues - v59
- Fixed new groups not being recognized - v60
- Added in precise grid velocity methods for better leading - v61
- Added in even more rocket estimation curves for different ranges for better lead estimation up close - v62
- Fix for code running multiple times per tick in DS - v63

To-do list:
- Add in custom bullet velocity arguments
*/