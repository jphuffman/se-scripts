/*
 * This script is ment to help players finely tune their thrust overrides 
 * and give them an easy way to toggle it on and off
 * 
 * Setup
 * 1. Create a thruster group and name it "Cruise Thrusters"
 * 2. Place the programmable block with this script on the hotbar using the "run command"
 * 3. have fun out there!
 * 
 */

private const string cruiseThrusters = "Cruise Thrusters"; // you can change this to another name if you wish

private bool cruiseState = false;
private float overrideAmount = 10; // tweek this for improved performance

public void Main(string args)
{
    IMyBlockGroup thrusterGroup = GridTerminalSystem.GetBlockGroupWithName(cruiseThrusters);
    List<IMyThrust> thrusters = new List<IMyThrust>();
    thrusterGroup.GetBlocksOfType<IMyThrust>(thrusters);

    if (!cruiseState)
    {
        Echo("Cruse Active");
        foreach (IMyThrust thruster in thrusters)
        {
            thruster.SetValueFloat("Override", overrideAmount);
        }
    }
    else
    {
        Echo("Cruse Offline");
        foreach (IMyThrust thruster in thrusters)
        {
            thruster.SetValueFloat("Override", 0);
        }
    }
    cruiseState = !cruiseState;
}