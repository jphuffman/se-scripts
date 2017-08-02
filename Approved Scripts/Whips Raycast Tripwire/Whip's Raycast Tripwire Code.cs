/* 
/// Whip's Raycast Tripwire Code v4 - 7/11/17 /// 
_____________________________________________________________________________________________________ 
///DESCRIPTION/// 
 
    The code uses camera(s) to serve as tripwires to trigger warheads and timers. The code will  
grab all warheads on the grid as this is designed for torpedo systems. You only need to name 
the cameras and (optionally) the timers. 
 
    You can configure the range of the tripwire below in the VARIABLES section. Also the code will 
ignore planets and friendly targets by default, but you can change this behavior as well in the  
VARIABLES section. 
 
    This code also has a minimum arming time where the tripwires will be inactive. This time is counted 
after the first triggering of this code. 
_____________________________________________________________________________________________________ 
///INSTRUCTIONS/// 
 
1.) Place this code in a program block 
2.) Make a timer block with the actions: 
    - Trigger Now itself 
    - Start itself 
    - Run this program 
3.) Add "Tripwire" into the name of your camera 
4.) (Optional): Add "Tripwire" to the name of any timer you want triggered when the tripwire is tripped 
5.) Trigger the timer to begin the arming sequence 
 
*/ 
 
//=================================================================================================== 
// VARIABLES - You can modify these 
//=================================================================================================== 
const string cameraName = "Tripwire"; //name of cameras to serve as tripwires 
const string timerName = "Tripwire"; //Name of timers to be triggered on tripwire being crossed 
const double range = 4; //range of tripwire (forward of camera's face) 
const double minumumArmTime = 3; //time after first triggering that the tripwire cameras will not be armed 
const bool ignorePlanetSurface = true; //if the code should ignore planet surfaces 
const bool ignoreFriends = true; //if the code should ignore friendlies 
 
//=================================================================================================== 
// DO NOT TOUCH ANYTHING BELOW // DO NOT TOUCH ANYTHING BELOW // DO NOT TOUCH ANYTHING BELOW // 
//=================================================================================================== 
 
double currentTimeElapsed = 0; 
 
void Main() 
{ 
    if (currentTimeElapsed < minumumArmTime) 
    { 
        currentTimeElapsed += Runtime.TimeSinceLastRun.TotalSeconds; 
        Echo($"Arming... \nTime Left: {minumumArmTime - currentTimeElapsed}"); 
        return; 
    } 
 
    Echo("< Tripwire Armed >"); 
    Echo($"Range: {range} m"); 
 
    var cameras = new List<IMyCameraBlock>(); 
    GridTerminalSystem.GetBlocksOfType(cameras, block => block.CustomName.Contains(cameraName)); 
 
    if (cameras.Count == 0) 
    { 
        Echo($"Error: No camera named '{cameraName}' was found"); 
        return; 
    } 
    Echo($"Camera Count: {cameras.Count}"); 
 
    foreach (IMyCameraBlock thisCamera in cameras) 
    { 
        thisCamera.EnableRaycast = true; 
		if(!thisCamera.CanScan(range)) continue;
		
        var targetInfo = thisCamera.Raycast(range); 
 
        if (targetInfo.IsEmpty()) 
        { 
            Echo("No target detected"); 
            continue; 
        } 
        else if (ignorePlanetSurface && targetInfo.Type.ToString() == "Planet") 
        { 
            Echo("Planet detected\nIgnoring..."); 
            continue; 
        } 
        else if(ignoreFriends && (targetInfo.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare || targetInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Owner)) 
        { 
            Echo("Friendly detected\nIgnoring..."); 
            continue; 
        } 
        else 
        { 
            Echo("Target detected"); 
            Detonate(); 
            Trigger(); 
            return; 
        } 
    } 
} 
 
void Detonate() 
{ 
    var warheads = new List<IMyWarhead>(); 
    GridTerminalSystem.GetBlocksOfType<IMyWarhead>(warheads); 
    foreach (IMyWarhead thisWarhead in warheads) 
    { 
        thisWarhead.SetValue<bool>("Safety", true); 
 
        if (thisWarhead.CustomName.ToLower().Contains("start")) 
            thisWarhead.ApplyAction("StartCountdown"); 
        else 
            thisWarhead.ApplyAction("Detonate"); 
    } 
} 
 
void Trigger() 
{ 
    var timers = new List<IMyTimerBlock>(); 
    GridTerminalSystem.GetBlocksOfType(timers, block => block.CustomName.Contains(timerName)); 
    foreach (IMyTimerBlock thisTimer in timers) 
    { 
        thisTimer.ApplyAction("TriggerNow"); 
    } 
} 
