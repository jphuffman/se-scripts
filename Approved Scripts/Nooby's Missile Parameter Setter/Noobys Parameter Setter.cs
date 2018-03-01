public Program()
{
Me.CustomData = ("detachThrustTag = Detach\nshooterReferenceName = Shooter Reference\nupdatesPerSecond = 20\ndisconnectDelay = 1\nguidanceDelay = 1\ndetachDuration = 0\nmainIgnitionDelay = 0\ndriftCompensation = True\nenableSpiralTrajectory = False\nspiralDegrees = 5\ntimeMaxSpiral = 3\nproportionalConstant = 50\nderivativeConstant = 20\noffsetUp = 0\noffsetLeft = 0\nmissileSpinRPM = 0\nlockVectorOnLaunch = False\n");

}



public void Main(string argument, UpdateType updateSource)
{

// Name of group of program blocks custom data to change
string GroupName = "ProgramDataEdit";

string MainProgramCustomData = Me.CustomData;
var group = GridTerminalSystem.GetBlockGroupWithName(GroupName);
if (group != null)
{
    var groupBlocks = new List<IMyTerminalBlock>();
    group.GetBlocks(groupBlocks);
	
	foreach(var block in groupBlocks)
{
		var input = block.CustomName;
		var splitted = input.Split(new[] { ' ' }, 2);
		Echo(splitted[0]);
		string DataToWrite = "MissileTag = " + splitted[0] + "\n" + MainProgramCustomData;
		block.CustomData = DataToWrite;
}	
}
}