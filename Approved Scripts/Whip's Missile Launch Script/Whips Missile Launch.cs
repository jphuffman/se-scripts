/* 
//// Whip's Missile Fire Script //// - revision 08 - 6/16/16 
*/ 
 
//Name of missile 
const string missileNameStr = "Missile"; 
 
Dictionary<IMyTerminalBlock, double> timerDictionary = new Dictionary<IMyTerminalBlock, double>(); 
double timeElapsed = 0; 
 
void Main(string arg) 
{ 
    Echo("WMI Missile Fire System\n"); 
    timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds; 
 
    switch (arg.ToLower()) 
    { 
        case "fire": 
            FireNextMissile(); 
            timeElapsed = 0; 
            break; 
 
        default: 
            break; 
    } 
} 
 
void FireNextMissile() 
{ 
    List<IMyTerminalBlock> missileTimerList = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.SearchBlocksOfName(missileNameStr, missileTimerList, IsTimer); 
 
    List<IMyTerminalBlock> keyList = new List<IMyTerminalBlock>(timerDictionary.Keys); 
     
    for (int i = 0; i < keyList.Count; i++) 
    { 
	    if (timerDictionary.ContainsKey(keyList[i])) 
        { 
            double timerCount; 
 
            timerDictionary.TryGetValue(keyList[i], out timerCount); 
            timerDictionary.Remove(keyList[i]); 
             
            timerCount += timeElapsed; //add time elapsed to the cound 
             
            if (timerCount < 1) //if not fired 
            { 
                timerDictionary.Add(keyList[i], timerCount); //add back to dict 
            } 
        } 
    } 
 
    for (int i = 0; i < missileTimerList.Count; i++) 
    { 
        var thisTimer = missileTimerList[i] as IMyTimerBlock; 
        if (timerDictionary.ContainsKey(thisTimer)) //removing missiles that have already fired 
        {             
            //missile has not detached yet 
            Echo(thisTimer.CustomName + " was already triggered; skipping"); 
            continue; 
        } 
        else 
        { 
            Echo("Triggered " + thisTimer.CustomName); 
            thisTimer.ApplyAction("TriggerNow"); //timer has not been triggered yet 
            timerDictionary.Add(thisTimer, 0); 
            break; 
        } 
    } 
} 
 
bool IsTimer(IMyTerminalBlock test_block) //checks if block is timer 
{ 
    var cast_timer = test_block as IMyTimerBlock; 
    return cast_timer != null; 
}