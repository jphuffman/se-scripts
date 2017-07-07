/// Whip's Warhead Detonator v1 /// - revision: 7/25/16 
 
void Main() 
{ 
    var warheads = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.GetBlocksOfType<IMyWarhead>(warheads); //grabs every warhead on grid 
    foreach (IMyWarhead thisWarhead in warheads) 
    { 
        thisWarhead.SetValue("Safety", true); //because keen is stupid and turning safety on ARMS the warhead :|
        thisWarhead.ApplyAction("Detonate"); 
    } 
}
