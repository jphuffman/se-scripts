/*
 * FurtherVs friendly inventory manager
 * 
 * This script either sorts all items into their respective containers [CURRENTLY NOT INCLUDED] OR counts all ingots and prints them onto an LCD
 *
 * SETUP:
 * 1. Stick this script on a programmable block
 * 2. Put LCDs and Blocks with an Inventory that should be searched in a Block Group called [FIM].
 * 3. Run the script using a terminal block (like a sensor, a timer or a button panel) or manually using one of the following arguments. BTW All other methods won't work.
 * 4. Enter the subtype of the items you want displayed in the customData of the LCD. Example: one LCD containing ore in the customData will only display Ores.
 * 
 * ARGUMENTS:
 * sort :   Sorts Items [CURRENTLY NOT INCLUDED]
 * count:   Counts Items.
 */
//Configuration Section
String GENERAL_TAG = "[FIM]";

Boolean debug = false;

//Do not touchy section

Dictionary<String, List<IMyTerminalBlock>> typeCargoDictionary = new Dictionary<string, List<IMyTerminalBlock>>();

Dictionary<String, double> oreTemplate;
Dictionary<String, double> ingotTemplate;
Dictionary<String, double> componentTemplate;

String[] typeList = { "component", "ingot", "ore", "physicalgunobject", "ammomagazine", "oxygencontainerobject", "gascontainerobject" };

List<IMyTextPanel> outputLCDList = new List<IMyTextPanel>();
List<IMyTextPanel> debugLCDList = new List<IMyTextPanel>();
List<IMyTerminalBlock> cargoBlocks = new List<IMyTerminalBlock>();



double lastRun = 3;

public Program()
{
    if (!Me.CustomName.EndsWith(GENERAL_TAG))
    {
        Me.CustomName += " " + GENERAL_TAG;
    }
    Echo("This Script can only be run manually");
    findBlocks();
    Me.CustomData = "Possible Item Types: component, ingot, ore,\nphysicalgunobject, ammomagazine, oxygencontainerobject,\ngascontainerobject\n\n";

    String[] oreArray = {"Stone","Iron","Nickel","Cobalt","Silicon","Silver","Gold","Uranium","Platinum","Ice" };
    String[] ingotArray = { "Stone", "Iron", "Nickel", "Cobalt", "Silicon", "Silver", "Gold", "Uranium", "Platinum" };
    String[] componentArray = { "SteelPlate","InteriorPlate", "Construction", "Medical", "Detector", "SolarCell", "Motor", "Explosives", "BulletproofGlass", "Computer", "LargeTube", "Girder", "Display", "SmallTube", "MetalGrid", "Canvas", "Reactor", "PowerCell", "Thrust", };

    oreTemplate = new Dictionary<string, double>();
    foreach (var entry in oreArray)
    {
        oreTemplate.Add(entry, 0);
    }

    ingotTemplate = new Dictionary<string, double>();

    foreach (var entry in ingotArray)
    {
        ingotTemplate.Add(entry, 0);
    }

    componentTemplate = new Dictionary<string, double>();
        foreach (var entry in componentArray)
    {
        componentTemplate.Add(entry, 0);
    }


    //CustomData Parsing for sorting
    //Sort Cargos in Categories
    foreach (var entry in cargoBlocks)
    {
        if (entry.HasInventory && entry is IMyCargoContainer)
        {
            var cc = (IMyTerminalBlock)entry;
            String customDataContent = cc.CustomData;
            if (String.IsNullOrEmpty(customDataContent)) continue;
            String[] subStrings = customDataContent.Split(';');
            foreach (var subString in subStrings)
            {
                if (isItemType(subString))
                {
                    if (typeCargoDictionary.ContainsKey(subString))
                    {
                        List<IMyTerminalBlock> listFromDictionary;
                        Boolean worked = typeCargoDictionary.TryGetValue(subString, out listFromDictionary);
                        if (!worked) continue;
                        listFromDictionary.Add(cc);
                    }
                    else
                    {
                        List<IMyTerminalBlock> listForDictionary = new List<IMyTerminalBlock>();
                        listForDictionary.Add(cc);
                        typeCargoDictionary[subString] = listForDictionary;
                    }
                }
            }
        }
    }
}

void cooldown()
{

}

public void Main(string argument, UpdateType updateSource)
{
    if((Runtime.TimeSinceLastRun.TotalSeconds + lastRun) < 2)
    {
        Echo("Script on cooldown...");
        lastRun += Runtime.TimeSinceLastRun.TotalSeconds;
        Echo("" + lastRun);
        return;
    }
    if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
    {
        if (outputLCDList.Count == 0 || cargoBlocks.Count == 0)
        {
            findBlocks();   
        }
        if (argument.ToLowerInvariant().Equals("sort"))
        {
            //Do Sorting
            clearDebugLCD();
            sort();
            cooldown();
            Echo("");
            lastRun = 0;
        }
        if (argument.ToLowerInvariant().Equals("count"))
        {
            //Do Counting
            foreach (var lcd in outputLCDList)
            {
                if(lcd==null || !lcd.IsFunctional)
                {
                    continue;
                }
                String type = "ingot";
                if (!String.IsNullOrEmpty(lcd.CustomData))
                {
                    if(lcd.CustomData.Contains("ore") || lcd.CustomData.Contains("ingot") || lcd.CustomData.Contains("component") || lcd.CustomData.Contains("ammomagazine") || lcd.CustomData.Contains("physicalgunobject"))
                    {
                        type = lcd.CustomData;
                    }
                }
                count(type, lcd);
                lastRun = 3;
            }
            cooldown();
            Echo("Counted Items.");
        }
        return;
    }
}

void sort()
{
    //Create Dictionary

    int itemstackCount = 0;

    //Move Items into Containers
    foreach (var entry in cargoBlocks)
    {
        if (entry == null) continue;
        if(entry.HasInventory)
        {
            var cc = (IMyTerminalBlock)entry;
            List<IMyInventoryItem> itemList = entry.GetInventory(0).GetItems();
            for(int itemSlotIndex = itemList.Count-1; itemSlotIndex >= 0; itemSlotIndex--)
            {
                IMyInventoryItem item = itemList[itemSlotIndex];
                String itemType = toItemType(item.Content.TypeId.ToString()).ToLowerInvariant();
                //Me.CustomData += itemType + "\n";
                if (!typeCargoDictionary.ContainsKey(itemType))
                {
                    writeToAllDebugLCD($"[ERROR] - Failed to Move {itemType}\n");
                    continue;
                }
                List<IMyTerminalBlock> destinationList = typeCargoDictionary[itemType];
                if (destinationList != null)
                {
                    if (destinationList.Count > 0)
                    {
                        int destinationIndex = 0;
                        int maxIndex = destinationList.Count - 1;
                        IMyTerminalBlock destination = destinationList[destinationIndex];
                        if (destination.Equals(cc)) continue;
						if(destinationList.Contains(cc)) continue;
                        Boolean success = false;
                        while(!success && destinationIndex <= maxIndex)
                        {
                            destination = destinationList[destinationIndex];
                            if (!destination.HasInventory)
                            {
                                destinationIndex++;
                                continue;
                            }
                            if(destination == null)
                            {
                                destinationIndex++;
                                continue;
                            }
                            success = destination.GetInventory().TransferItemFrom(cc.GetInventory(), itemSlotIndex, null, true, null);
                            writeToAllDebugLCD($"[TRY] - Moved {itemType} from {cc.CustomName} to {destination.CustomName}\n");
                            destinationIndex++;
                        }
                        if (success) itemstackCount++;
                        if (success) writeToAllDebugLCD($"[SUCCESS] - Moved {itemType} from {cc.CustomName} to {destination.CustomName}\n");
                    }
                }

            }
        }
    }
    Echo($"Sorted {itemstackCount} Items!");
}

Dictionary<String, double> getTemplateDictionary(String type)
{
    if (type.ToLowerInvariant().Equals("ore"))
    {

        return new Dictionary<String, double>(oreTemplate);
    }
    if (type.ToLowerInvariant().Equals("ingot"))
    {

        return new Dictionary<String, double>(ingotTemplate);
    }
    if (type.ToLowerInvariant().Equals("component"))
    {

        return new Dictionary<String, double>(componentTemplate);
    }
    return new Dictionary<string, double>();
}

void count(String type, IMyTextPanel lcd)
{
    Dictionary<String, double> itemDictionary = getTemplateDictionary(type);

    //Loop through all tagged blocks
    foreach (var block in cargoBlocks)
    {
        if(block != null && block.IsWorking)
        {
            IMyInventory inventory = block.GetInventory(0);
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
        }
    }

    int largestKey = 10;
    foreach (var key in itemDictionary.Keys)
    {
        if (key.Length > largestKey)
        {
            largestKey = key.Length + 1;
        }
    }

    //Print Results to LCD
    String output = "";
    output += centerText($"<--- {type.ToUpperInvariant()} --->", lcd.FontSize) + "\n\n";
    foreach (var key in itemDictionary.Keys)
    {
        string toAdd = $"{key}";
        while (toAdd.Length < largestKey)
        {
            toAdd += " ";
        }
        output += $"{toAdd}: {betterOutput(Math.Round(itemDictionary[key],2))}\n";

    }
    //lcd.Enabled = true;
    lcd.ShowPublicTextOnScreen();
    lcd.WritePublicText(output);
    lcd.Font = "Monospace";
    //lcd.FontSize = (float)0.8;
}

string toItemType(String s)
{
    return s.Replace("MyObjectBuilder_", "");
}

string betterOutput(double d)
{
    if (d > 1000 && d < 1000000)
    {
        d /= 1000;
        d = Math.Round(d, 2);
        return d.ToString() + "k";
    }
    if (d > 1000000)
    {
        d /= 1000000;
        d = Math.Round(d, 2);
        return d.ToString() + "M";
    }
    return d.ToString();
}

void writeToAllDebugLCD(String s)
{
    if (debugLCDList.Count == 0) return;
    if (!debug) return;
    foreach (var debugLCD in debugLCDList)
    {
        debugLCD.WritePublicText(s, true);
    }
}

void clearDebugLCD()
{
    if (debugLCDList.Count == 0) return;
    if (!debug) return;
    foreach (var debugLCD in debugLCDList)
    {
        debugLCD.WritePublicText("", false);
    }
}

string centerText(String s, float fontSize)
{
    int sizeAtDefault = 27;
    float defaultSize = 1;
    float multiplier = 1;
    if(fontSize < 1)
    {
        multiplier = (defaultSize - fontSize) + 1;
    }
    if(fontSize > 1)
    {
        multiplier = (defaultSize - fontSize) + 1;
    }
    double calculatedSize = sizeAtDefault * multiplier;
    int maxSize = (int) Math.Floor(calculatedSize);
    if (s.Length > maxSize) return s;
    var blankText = new string(' ', (maxSize - s.Length) / 2);
    string printLine = blankText + s;
    return printLine;

}

Boolean isItemType(String s)
{
    return typeList.Contains(s.ToLowerInvariant());
}

void findBlocks()
{
    IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(GENERAL_TAG);
    if (group != null)
    {
        outputLCDList.Clear();
        cargoBlocks.Clear();

        group.GetBlocksOfType<IMyTextPanel>(outputLCDList, (IMyTextPanel x) => x.IsFunctional && x.CustomData!="debug");
        group.GetBlocksOfType<IMyTextPanel>(debugLCDList, (IMyTextPanel x) => x.IsFunctional && x.CustomData == "debug");
        group.GetBlocksOfType<IMyTerminalBlock>(cargoBlocks, (IMyTerminalBlock x) => x.HasInventory);

    } else
    {
        Echo("ERROR: Block Group not found!");
        return;
    }
}

void log(String s)
{
    Me.CustomData += s + "\n";
}
