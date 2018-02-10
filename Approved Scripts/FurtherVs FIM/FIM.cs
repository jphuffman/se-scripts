/*
 * FurtherVs friendly inventory manager
 * 
 * This script either sorts all items into their respective containers [CURRENTLY NOT INCLUDED] OR counts all ingots and prints them onto an LCD
 *
 * SETUP:
 * 1. Stick this script on a programmable block
 * 2. Put LCDs and Blocks with an Inventory that should be searched in a Block Group called [FIM].
 * 3. Run the script using a terminal block (like a sensor, a timer or a button panel) or manually using one of the following arguments. BTW All other methods won't work.
 * 
 * ARGUMENTS:
 * sort :   Sorts Items [CURRENTLY NOT INCLUDED]
 * count:   Counts Items. Accepts a second Argument that defines the exact subtype which should be listed. (component, ore, ingot).
 *          Usage: count OR count,subtype
 */
//Configuration Section
String GENERAL_TAG = "[FIM]";

double deltaTime = 0;
long scriptTime  = 0; //# of seconds that the script has been running.
double subSecond = 0;
long lastRuntime = 0;

//Do not touchy section

List<IMyTextPanel> outputLCDList = new List<IMyTextPanel>();
List<IMyTerminalBlock> cargoBlocks = new List<IMyTerminalBlock>();

public Program()
{
    if (!Me.CustomName.EndsWith(GENERAL_TAG))
    {
        Me.CustomName += " " + GENERAL_TAG;
    }
    Echo("This Script can only be run manually");
    findBlocks();
}

public void Main(string argument, UpdateType updateSource)
{
	if (Runtime.TimeSinceLastRun.TotalSeconds != 0) //This is for timekeeping
	{
		deltaTime = Runtime.TimeSinceLastRun.TotalSeconds;
		
		subSecond += deltaTime;

		if ((int)subSecond > 0) {
			//Having a long for scriptTime allows per-second accuracy for many many consecutive days;
			//having subSecond be a double also allows for high accuracy...
			//this will use 12 bytes for timekeeping
			scriptTime += (int)(subSecond);
			subSecond -= (int)(subSecond);
		}
	}
	
	
    if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
    {
		if(scriptTime > lastRunTime + 10) //Checking the run frequency
			lastRuntime = scriptTime;
		else
			return;
		
		
        if(outputLCDList.Count == 0 || cargoBlocks.Count == 0)
        {
            findBlocks();
        }
        if (argument.ToLowerInvariant().Contains("sort"))
        {
            //Do Sorting
            sort();
        }
        if (argument.ToLowerInvariant().Contains("count"))
        {
            //Do Counting
            String[] argumentSplit = argument.Split(',');
            String type = "ingot";
            if(argumentSplit.Count() > 1)
            {
                type = argumentSplit[1];
            }
            count(type);
        }
        return;
    }
}

void sort()
{
    //TODO: Add stuff
}

void count(String type)
{
    Dictionary<String, double> itemDictionary = new Dictionary<string, double>();
    bool getNewBlocks = false;

    //Loop through all tagged blocks
    foreach (var block in cargoBlocks)
    {
        if(block != null && block.IsWorking)
        {
            IMyInventory inventory = block.GetInventory();
            List<IMyInventoryItem> items = inventory.GetItems();
            foreach (var item in items)
            {
                if (item.Content.ToString().ToLowerInvariant().Contains(type.ToLowerInvariant()))
                {
                    if (!itemDictionary.ContainsKey(item.Content.SubtypeName))
                    {
                        itemDictionary[item.Content.SubtypeName] = (double)item.Amount;
                    } else
                    {
                        itemDictionary[item.Content.SubtypeName] += (double)item.Amount;
                    }
                }
            }
        } else
        {
            getNewBlocks = true;
        }
    }

    //Print Results to LCD
    String output = "";
    foreach (var key in itemDictionary.Keys)
    {
        output += $"{key}: {Math.Round(itemDictionary[key],2)}\n";
    }
    foreach (var lcd in outputLCDList)
    {
        if(lcd!=null && lcd.IsWorking)
        {
            lcd.Enabled = true;
            lcd.ShowPublicTextOnScreen();
            lcd.WritePublicText(output);
        } else
        {
            getNewBlocks = true;
        }
    }

    //At the End
    if (getNewBlocks)
    {
        findBlocks();
    }
}

void findBlocks()
{
    IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(GENERAL_TAG);
    if (group != null)
    {
        outputLCDList.Clear();
        cargoBlocks.Clear();
        group.GetBlocksOfType<IMyTextPanel>(outputLCDList, (IMyTextPanel x) => x.IsWorking);
        group.GetBlocksOfType<IMyTerminalBlock>(cargoBlocks, (IMyTerminalBlock x) => x.HasInventory);
    } else
    {
        Echo("ERROR: Block Group not found!");
        return;
    }
}
