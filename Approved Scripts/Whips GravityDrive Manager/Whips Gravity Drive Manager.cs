/*
/// Whip's Directional Gravity Drive Control Script v8 - 7/24/17 ///
________________________________________________
Description:

    This code allows you to control a gravity drive through normal movement keys.
    The setup is INCREDIBLY simple!

    DISCLAIMER: This code is NOT made for planerary flight as grav drives do not work in natural gravity.
________________________________________________
How do I use this?

    1) Make sure you have at least 1 thruster in each direction that your grav drive
    should control. I suggest you use Ion engines

    2) Place this program on your main grid (the grid your control seat is on)

    3) Make a timer block with actions:
    - "Run" this program with NO ARGUMENT
    - "Trigger Now" itself 
    - "Start" itself 

    4) Make a group with all of your gravity drive artificial masses and gravity gens. Name it "Gravity Drive"

    5) Trigger the timer

    6) Enjoy!
________________________________________________
Arguments

    on : Turns grav drive on
    off : Turns grav drive off
    toggle : toggles grav drive
________________________________________________
Author's Notes

    This code was written pon request of my friend Avalash for his big cool carrier thing. I've decided to polish this code
    and release it to the public. Leave any questions, comments, or converns on the workshop page!

- Whiplash141
*/

const string gravityDriveGroupName = "Gravity Drive";
//place all gravity drive generators and masses in this group

float gravityDriveDampenerScalingFactor = 0.1f;
//larger values will quicken the dampening using gravity gens but will also risk causing oscillations
//The lighter your ship, the smaller this should be

//-------------------------------------------------------------------------
//============ NO TOUCH BELOW HERE!!! =====================================
//-------------------------------------------------------------------------

const double updatesPerSecond = 10;
const double timeMaxCycle = 1 / updatesPerSecond;
//const float stepRatio = 0.1f; //this is the ratio of the max acceleration to add each code cycle
double timeCurrentCycle = 0;

const double refreshInterval = 10;
double refreshTime = 141;

bool turnOn = true;
bool isSetup = false;

void Main(string arg)
{
    switch (arg.ToLower())
    {
        case "on":
            turnOn = true;
            break;

        case "off":
            turnOn = false;
            break;

        case "toggle":
            turnOn = !turnOn; //switches boolean value
            break;
    }

    timeCurrentCycle += Runtime.TimeSinceLastRun.TotalSeconds;
    refreshTime += Runtime.TimeSinceLastRun.TotalSeconds;
    try
    {
        if (!isSetup || refreshTime >= refreshInterval)
        {
            isSetup = GrabBlocks();
            refreshTime = 0;
        }

        if (!isSetup)
            return;

        if (timeCurrentCycle >= timeMaxCycle)
        {
            Echo("WMI Gravity Drive Manager... " + RunningSymbol());
            
            Echo($"Next refresh in {Math.Max(refreshInterval - refreshTime, 0):N0} seconds");

            if (turnOn)
                Echo("\nGravity Drive is Enabled");
            else
                Echo("\nGravity Drive is Disabled");

            Echo($"\nGravity Drive Stats:\n Artificial Masses: {artMasses.Count}\n Gravity Generators:\n >Forward: {fowardGens.Count}\n >Backward: {backwardGens.Count}\n >Left: {leftGens.Count}\n >Right: {rightGens.Count}\n >Up: {upGens.Count}\n >Down: {downGens.Count}\n >Other: {otherGens.Count}");

            ManageGravDrive(turnOn);
            timeCurrentCycle = 0;

            runningSymbolVariant++;
        }
    }
    catch
    {
        isSetup = false;
    }
}

//Whip's Running Symbol Method v6
int runningSymbolVariant = 0;
string RunningSymbol()
{
    string strRunningSymbol = "";

    if (runningSymbolVariant == 0)
        strRunningSymbol = "|";
    else if (runningSymbolVariant == 1)
        strRunningSymbol = "/";
    else if (runningSymbolVariant == 2)
        strRunningSymbol = "--";
    else if (runningSymbolVariant == 3)
    {
        strRunningSymbol = "\\";
        runningSymbolVariant = 0;
    }

    return strRunningSymbol;
}


List<IMyShipController> shipControllers = new List<IMyShipController>();
List<IMyGravityGenerator> gravityGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> upGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> downGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> leftGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> rightGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> fowardGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> backwardGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> otherGens = new List<IMyGravityGenerator>();
List<List<IMyGravityGenerator>> gravityList = new List<List<IMyGravityGenerator>>();
List<IMyVirtualMass> artMasses = new List<IMyVirtualMass>();
List<IMyThrust> onGridThrust = new List<IMyThrust>();
IMyBlockGroup gravityDriveGroup = null;




bool GrabBlocks()
{
    shipControllers.Clear();
    gravityGens.Clear();
    upGens.Clear();
    downGens.Clear();
    leftGens.Clear();
    rightGens.Clear();
    fowardGens.Clear();
    backwardGens.Clear();
    otherGens.Clear();
    artMasses.Clear();
    gravityList.Clear();
    gravityDriveGroup = null;

    GridTerminalSystem.GetBlocksOfType(shipControllers, block => block.CubeGrid == Me.CubeGrid); //makes sure controller is on same grid
    GridTerminalSystem.GetBlocksOfType(onGridThrust, block => block.CubeGrid == Me.CubeGrid);
    gravityDriveGroup = GridTerminalSystem.GetBlockGroupWithName(gravityDriveGroupName);

    #region block_check
    bool critFailure = false;
    if (gravityDriveGroup == null)
    {
        Echo($"Critical Error: No group named {gravityDriveGroupName} was found");
        critFailure = true;
    }
    else
    {
        gravityDriveGroup.GetBlocksOfType(artMasses);
        gravityDriveGroup.GetBlocksOfType(gravityGens, x => x.CubeGrid == Me.CubeGrid);
        gravityDriveGroup.GetBlocksOfType(otherGens, x => x.CubeGrid != Me.CubeGrid);
    }

    if (artMasses.Count == 0)
    {
        Echo($"Critical Error: No artificial masses found in the {gravityDriveGroupName} group");
        critFailure = true;
    }

    if (gravityGens.Count == 0)
    {
        Echo($"Critical Error: No gravity generators found in the {gravityDriveGroupName} group");
        critFailure = true;
    }

    if (shipControllers.Count == 0)
    {
        Echo("Critical Error: No ship controllers found on the grid");
        critFailure = true;
    }
    else
    {
        var controller = shipControllers[0];
        foreach (var block in gravityGens)
        {
            if (controller.WorldMatrix.Forward == block.WorldMatrix.Down)
                fowardGens.Add(block);
            else if (controller.WorldMatrix.Backward == block.WorldMatrix.Down)
                backwardGens.Add(block);
            else if (controller.WorldMatrix.Left == block.WorldMatrix.Down)
                leftGens.Add(block);
            else if (controller.WorldMatrix.Right == block.WorldMatrix.Down)
                rightGens.Add(block);
            else if (controller.WorldMatrix.Up == block.WorldMatrix.Down)
                upGens.Add(block);
            else if (controller.WorldMatrix.Down == block.WorldMatrix.Down)
                downGens.Add(block);
        }

        gravityList.Add(fowardGens);
        gravityList.Add(backwardGens);
        gravityList.Add(leftGens);
        gravityList.Add(rightGens);
        gravityList.Add(upGens);
        gravityList.Add(downGens);
    }

    return !critFailure;
    #endregion
}

void ManageGravDrive(bool turnOn)
{
    IMyShipController reference = GetControlledShipController(shipControllers);

    //Desired travel vector construction
    var referenceMatrix = reference.WorldMatrix;
    var inputVec = reference.MoveIndicator; //raw input vector     
    var desiredDirection = referenceMatrix.Backward * inputVec.Z + referenceMatrix.Right * inputVec.X + referenceMatrix.Up * inputVec.Y; //world relative input vector
    if (desiredDirection.LengthSquared() > 0)
    {
        desiredDirection = Vector3D.Normalize(desiredDirection);
    }

    var velocityVec = reference.GetShipVelocities().LinearVelocity;

    bool dampenersOn = reference.DampenersOverride;
    if (onGridThrust.Count == 0)
    {
        dampenersOn = true;
    }

    if (velocityVec.LengthSquared() < 0.1 * 0.1)
    {
        ToggleMass(artMasses, false);
        ToggleDirectionalGravity(gravityGens, desiredDirection, velocityVec, false, dampenersOn);
    }
    else
    {
        ToggleMass(artMasses, true); //default all masses to turn on
        ToggleDirectionalGravity(gravityGens, desiredDirection, velocityVec, turnOn, dampenersOn); //add toggle for on off state
    }
}

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return SCs[0];
}

void ToggleDirectionalGravity(List<IMyGravityGenerator> gravGens, Vector3D direction, Vector3D velocityVec, bool turnOn, bool dampenersOn = true)
{
    //Handle on grid grav gens
    foreach (var list in gravityList)
    {
        if (list.Count == 0)
            continue;

        var referenceGen = list[0];

        if (turnOn)
        {
            double gravThrustRatio = referenceGen.WorldMatrix.Down.Dot(direction);
            double gravDampingRatio;

            gravDampingRatio = referenceGen.WorldMatrix.Up.Dot(velocityVec);

            //list do this
            SetGravityAcceleration(list, (float)gravThrustRatio * 9.81f);
            //referenceGen.SetValue("Gravity", (float)gravThrustRatio * 9.81f);
            SetGravityPower(list, true);
            //referenceGen.SetValue("OnOff", true);

            if (dampenersOn)
            {
                double targetOverride = 0;

                if (Math.Abs(gravDampingRatio) < 1)
                    targetOverride = gravDampingRatio * gravityDriveDampenerScalingFactor;
                else
                    targetOverride = Math.Sign(gravDampingRatio) * gravDampingRatio * gravDampingRatio * gravityDriveDampenerScalingFactor;

                if (targetOverride < 0 && gravThrustRatio <= 0 && Math.Abs(targetOverride) > Math.Abs(gravThrustRatio))
                    SetGravityAcceleration(list, (float)targetOverride);
                    //thisGravGen.SetValue("Gravity", (float)targetOverride);
                else if (targetOverride > 0 && gravThrustRatio >= 0 && Math.Abs(targetOverride) > Math.Abs(gravThrustRatio))
                    SetGravityAcceleration(list, (float)targetOverride);
                    //thisGravGen.SetValue("Gravity", (float)targetOverride);
            }
        }
        else
        {
            SetGravityPower(list, false);
            /*
            bool isOn = thisGravGen.GetValue<bool>("OnOff");
            if (isOn) //is on but should be off
            {
                thisGravGen.ApplyAction("OnOff_Off");
                //thisGravGen.SetValue("Gravity", 0f);
            }*/
        }

    }

    //---Handle the rest of the off-grid gravity gens
    foreach (IMyGravityGenerator thisGravGen in otherGens)
    {
        if (turnOn)
        {
            double gravThrustRatio = thisGravGen.WorldMatrix.Down.Dot(direction);
            double gravDampingRatio;

            gravDampingRatio = thisGravGen.WorldMatrix.Up.Dot(velocityVec);

            thisGravGen.GravityAcceleration = (float)gravThrustRatio * 9.81f;
            thisGravGen.Enabled = true;

            if (dampenersOn)
            {
                double targetOverride = 0;

                if (Math.Abs(gravDampingRatio) < 1)
                    targetOverride = gravDampingRatio * gravityDriveDampenerScalingFactor;
                else
                    targetOverride = Math.Sign(gravDampingRatio) * gravDampingRatio * gravDampingRatio * gravityDriveDampenerScalingFactor;

                if (targetOverride < 0 && gravThrustRatio <= 0 && Math.Abs(targetOverride) > Math.Abs(gravThrustRatio))
                    thisGravGen.GravityAcceleration = (float)targetOverride;
                else if (targetOverride > 0 && gravThrustRatio >= 0 && Math.Abs(targetOverride) > Math.Abs(gravThrustRatio))
                    thisGravGen.GravityAcceleration = (float)targetOverride;
            }
        }
        else
        {
            thisGravGen.Enabled = false;
        }
    }
}

void SetGravityAcceleration(List<IMyGravityGenerator> list, float value)
{
    foreach (var block in list)
        block.GravityAcceleration = value;
}

void SetGravityPower(List<IMyGravityGenerator> list, bool value)
{
    foreach (var block in list)
        block.Enabled = value;
}

void ToggleMass(List<IMyVirtualMass> artMasses, bool toggleOn)
{
    foreach (IMyVirtualMass thisMass in artMasses)
    {
        bool isOn = thisMass.GetValue<bool>("OnOff");
        if (isOn == toggleOn) //state is same
        {
            continue;
        }
        else if (toggleOn) //is off but should be on
        {
            thisMass.ApplyAction("OnOff_On");
        }
        else //is on but should be off
        {
            thisMass.ApplyAction("OnOff_Off");
        }
    }
}

/*
/// CHANGE LOG ///
* Rewrote entire code to use direct user inputs - v5
* Added OnOff argument handling - v6
* Fixed dampeners not acting the same way in different directions - v7
* Optimized gravity gen calcs - v8
* Reduced block refreshing from 10 Hz to 0.1 Hz - v8
*/