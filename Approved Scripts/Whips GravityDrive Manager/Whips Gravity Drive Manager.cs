/*
/// Whip's Directional Gravity Drive Control Script v7 - 7/13/17 ///
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

//You can change the name of the group here
const string gravityDriveGroupTag = "Gravity Drive";
//place all gravity drive generators and masses in this group

//-------------------------------------------------------------------------
//============ NO TOUCH BELOW HERE!!! =====================================
//-------------------------------------------------------------------------

const double updatesPerSecond = 10;
const double timeMaxCycle = 1 / updatesPerSecond;
//const float stepRatio = 0.1f; //this is the ratio of the max acceleration to add each code cycle
double timeCurrentCycle = 0;

bool turnOn = true;

void Main(string arg)
{
    switch(arg.ToLower())
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

    if (timeCurrentCycle >= timeMaxCycle)
    {
        Echo("WMI Gravity Drive Manager Online... " + RunningSymbol());
        
        if (turnOn)
            Echo("\n Gravity Drive is Enabled");
        else
            Echo("\n Gravity Drive is Disabled");

        ManageGravDrive(turnOn);
        timeCurrentCycle = 0;

        runningSymbolVariant++;
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

void ManageGravDrive(bool turnOn)
{
    var shipControllers = new List<IMyShipController>();
    var gravityGens = new List<IMyGravityGenerator>();
    var artMasses = new List<IMyVirtualMass>();
    var groups = new List<IMyBlockGroup>();

    GridTerminalSystem.GetBlocksOfType(shipControllers, block => block.CubeGrid == Me.CubeGrid); //makes sure controller is on same grid
    GridTerminalSystem.GetBlockGroups(groups);

    IMyBlockGroup gravityDriveGroup = null;
    foreach (IMyBlockGroup thisGroup in groups)
    {
        if (thisGroup.Name.ToLower().Contains(gravityDriveGroupTag.ToLower()))
        {
            gravityDriveGroup = thisGroup;
            break;
        }
    }

    #region block_check
    bool critFailure = false;
    if (gravityDriveGroup == null)
    {
        Echo($"Critical Error: No group named {gravityDriveGroupTag} was found");
        critFailure = true;
    }
    else
    {
        gravityDriveGroup.GetBlocksOfType(artMasses);
        gravityDriveGroup.GetBlocksOfType(gravityGens);

        if (artMasses.Count == 0)
        {
            Echo($"Critical Error: No artificial masses found in the {gravityDriveGroupTag} group");
            critFailure = true;
        }

        if (gravityGens.Count == 0)
        {
            Echo($"Critical Error: No gravity generators found in the {gravityDriveGroupTag} group");
            critFailure = true;
        }
    }

    if (shipControllers.Count == 0)
    {
        Echo("Critical Error: No ship controllers found on the grid");
        critFailure = true;
    }

    if (critFailure)
    {
        return;
    }
    #endregion

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
    var onGridThrust = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType(onGridThrust, block => block.CubeGrid == Me.CubeGrid);
    if (onGridThrust.Count == 0)
    {
        dampenersOn = true;
    }
    
    if (velocityVec.LengthSquared() < 0.1*0.1)
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
    foreach (IMyGravityGenerator thisGravGen in gravGens)
    {
        if (turnOn)
        {
            double gravThrustRatio = thisGravGen.WorldMatrix.Down.Dot(direction);
            double gravDampingRatio;

            gravDampingRatio = thisGravGen.WorldMatrix.Up.Dot(velocityVec);

            thisGravGen.SetValue("Gravity", (float)gravThrustRatio * 9.81f);
            thisGravGen.SetValue("OnOff", true);

            if (dampenersOn)
            {
                double targetOverride = 0;

                if (Math.Abs(gravDampingRatio) < 1)
                    targetOverride = gravDampingRatio * 0.1;
                else
                    targetOverride = Math.Sign(gravDampingRatio) * gravDampingRatio * gravDampingRatio * 0.1;

                if (targetOverride < 0 && gravThrustRatio <= 0 && Math.Abs(targetOverride) > Math.Abs(gravThrustRatio))
                    thisGravGen.SetValue("Gravity", (float)targetOverride);
                else if (targetOverride > 0 && gravThrustRatio >= 0 && Math.Abs(targetOverride) > Math.Abs(gravThrustRatio))
                    thisGravGen.SetValue("Gravity", (float)targetOverride);
            }
            
        }
        else
        {
            bool isOn = thisGravGen.GetValue<bool>("OnOff");
            if (isOn) //is on but should be off
            {
                thisGravGen.ApplyAction("OnOff_Off");
                //thisGravGen.SetValue("Gravity", 0f);
            }
        }
    }
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
*/