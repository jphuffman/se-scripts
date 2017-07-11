//Whip's Profiler Graph Code
int count = 1;
int maxSeconds = 60;
StringBuilder profile = new StringBuilder();
void ProfilerGraph()
{
    if (count <= maxSeconds * 60)
    {
        double timeToRunCode = Runtime.LastRunTimeMs;

        profile.Append(timeToRunCode.ToString()).Append("\n");
        count++;
    }
    else
    {
        var screen = GridTerminalSystem.GetBlockWithName("DEBUG") as IMyTextPanel;
        screen?.WritePublicText(profile.ToString());
        screen?.ShowPublicTextOnScreen();
    }
}
