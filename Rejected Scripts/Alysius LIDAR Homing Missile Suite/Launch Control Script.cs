//------------------------------------------------------------
// ADN - Launch Control Script v1.3
//------------------------------------------------------------

static string strLaunchTimerLoopTag = "Missile Clock";  //Prefix name of the Timer Block to trigger. It will find one based on launchSelectionType
static string strBatteryNameTag = "";                   //Prefix name of the Battery to set to Recharge. Set blank to disable
static string strDetachConnectorTag = "";               //Prefix name of the Connector closest to the triggered Timer block to Unlock. Set blank to disable
static string strComputerTag = "Missile Computer";      //Prefix name of the Programmable Block closest to the triggered Timer block to inject Run Argument. Set blank to disable

int launchSelectionType = 2;                            //0 = Any, 1 = Closest, 2 = Furthest

//------------------------------ Below Is Main Script Body ------------------------------

void Main(string arguments)
{
    if (Me.CustomData.Length > 0)
    {
        ProcessCustomConfiguration();
    }

    if (strBatteryNameTag != null && strBatteryNameTag.Length > 0)
    {
        FixBatteries();
    }

    List<IMyTerminalBlock> blocks = GetBlocksWithName<IMyTimerBlock>(strLaunchTimerLoopTag);

    IMyTerminalBlock triggeredTimerBlock = null;

    switch (launchSelectionType)
    {
        case 1:
            triggeredTimerBlock = LaunchClosest(blocks);
            break;
        case 2:
            triggeredTimerBlock = LaunchFurthest(blocks);
            break;
        default:
            triggeredTimerBlock = LaunchAny(blocks);
            break;
    }

    if (triggeredTimerBlock != null)
    {
        if (strComputerTag != null && strComputerTag.Length > 0)
        {
            blocks = GetBlocksWithName<IMyProgrammableBlock>(strComputerTag);
            IMyTerminalBlock closestBlock = GetClosestBlockFromReference(blocks, triggeredTimerBlock);

            if (closestBlock != null)
            {
                List<TerminalActionParameter> parameters = new List<TerminalActionParameter>();
                parameters.Add(TerminalActionParameter.Get(arguments));
                closestBlock.ApplyAction("Run", parameters);
            }
        }

        if (strDetachConnectorTag != null && strDetachConnectorTag.Length > 0)
        {
            blocks = GetBlocksWithName<IMyShipConnector>(strDetachConnectorTag);
            IMyTerminalBlock closestBlock = GetClosestBlockFromReference(blocks, triggeredTimerBlock);

            if (closestBlock != null)
            {
                closestBlock.ApplyAction("Unlock");

                IMyShipConnector otherConnector = ((IMyShipConnector)closestBlock).OtherConnector;
                if (otherConnector != null)
                {
                    otherConnector.ApplyAction("Unlock");
                }
            }
        }
    }
}

void ProcessCustomConfiguration()
{
    CustomConfiguration cfg = new CustomConfiguration(Me);
    cfg.Load();

    cfg.Get("strLaunchTimerLoopTag", ref strLaunchTimerLoopTag);
    cfg.Get("strBatteryNameTag", ref strBatteryNameTag);
    cfg.Get("strDetachConnectorTag", ref strDetachConnectorTag);
    cfg.Get("strComputerTag", ref strComputerTag);
    cfg.Get("launchSelectionType", ref launchSelectionType);
}

void FixBatteries()
{
    List<IMyTerminalBlock> blocks = GetBlocksWithName<IMyBatteryBlock>(strBatteryNameTag);

    for (int i = 0; i < blocks.Count; i++)
    {
        blocks[i].SetValue<bool>("Recharge", false);
    }
}

IMyTerminalBlock LaunchAny(List<IMyTerminalBlock> blocks)
{
    if (blocks.Count > 0)
    {
        blocks[0].ApplyAction("TriggerNow");
        return blocks[0];
    }
    return null;
}

IMyTerminalBlock LaunchClosest(List<IMyTerminalBlock> blocks)
{
    double currDist = 0;
    double closestDist = Double.MaxValue;
    IMyTerminalBlock closestBlock = null;

    for (int i = 0; i < blocks.Count; i++)
    {
        currDist = (blocks[i].GetPosition() - Me.GetPosition()).Length();
        if (currDist < closestDist)
        {
            closestDist = currDist;
            closestBlock = blocks[i];
        }
    }

    if (closestBlock != null)
    {
        closestBlock.ApplyAction("TriggerNow");
    }
    return closestBlock;
}

IMyTerminalBlock LaunchFurthest(List<IMyTerminalBlock> blocks)
{
    double currDist = 0;
    double furthestDist = 0;
    IMyTerminalBlock furthestBlock = null;

    for (int i = 0; i < blocks.Count; i++)
    {
        currDist = (blocks[i].GetPosition() - Me.GetPosition()).Length();
        if (currDist > furthestDist)
        {
            furthestDist = currDist;
            furthestBlock = blocks[i];
        }
    }

    if (furthestBlock != null)
    {
        furthestBlock.ApplyAction("TriggerNow");
    }
    return furthestBlock;
}

IMyTerminalBlock GetClosestBlockFromReference(List<IMyTerminalBlock> checkBlocks, IMyTerminalBlock referenceBlock)
{
    IMyTerminalBlock checkBlock = null;
    double prevCheckDistance = Double.MaxValue;

    for (int i = 0; i < checkBlocks.Count; i++)
    {
        double currCheckDistance = (checkBlocks[i].GetPosition() - referenceBlock.GetPosition()).Length();
        if (currCheckDistance < prevCheckDistance)
        {
            prevCheckDistance = currCheckDistance;
            checkBlock = checkBlocks[i];
        }
    }

    return checkBlock;
}

//------------------------------ Misc API ------------------------------

List<IMyTerminalBlock> GetBlocksWithName<T>(string name, int matchType = 0) where T: class, IMyTerminalBlock
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(name, blocks);

    List<IMyTerminalBlock> filteredBlocks = new List<IMyTerminalBlock>();
    for (int i = 0; i < blocks.Count; i++)
    {
        if (matchType > 0)
        {
            bool isMatch = false;

            switch (matchType)
            {
                case 1:
                    if (blocks[i].CustomName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    break;
                case 2:
                    if (blocks[i].CustomName.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    break;
                case 3:
                    if (blocks[i].CustomName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    break;
                default:
                    isMatch = true;
                    break;
            }

            if (!isMatch)
            {
                continue;
            }
        }

        IMyTerminalBlock block = blocks[i] as T;
        if (block != null)
        {
            filteredBlocks.Add(block);
        }
    }

    return filteredBlocks;
}

public class CustomConfiguration
{
    public IMyTerminalBlock configBlock;
    public Dictionary<string, string> config;

    public CustomConfiguration(IMyTerminalBlock block)
    {
        configBlock = block;
        config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Load()
    {
        ParseCustomData(configBlock, config);
    }

    public void Save()
    {
        WriteCustomData(configBlock, config);
    }

    public string Get(string key, string defVal = null)
    {
        return config.GetValueOrDefault(key.Trim(), defVal);
    }

    public void Get(string key, ref string res)
    {
        string val;
        if (config.TryGetValue(key.Trim(), out val))
        {
            res = val;
        }
    }

    public void Get(string key, ref int res)
    {
        int val;
        if (int.TryParse(Get(key), out val))
        {
            res = val;
        }
    }

    public void Get(string key, ref float res)
    {
        float val;
        if (float.TryParse(Get(key), out val))
        {
            res = val;
        }
    }

    public void Get(string key, ref double res)
    {
        double val;
        if (double.TryParse(Get(key), out val))
        {
            res = val;
        }
    }

    public void Get(string key, ref bool res)
    {
        bool val;
        if (bool.TryParse(Get(key), out val))
        {
            res = val;
        }
    }
    public void Get(string key, ref bool? res)
    {
        bool val;
        if (bool.TryParse(Get(key), out val))
        {
            res = val;
        }
    }

    public void Set(string key, string value)
    {
        config[key.Trim()] = value;
    }

    public static void ParseCustomData(IMyTerminalBlock block, Dictionary<string, string> cfg, bool clr = true)
    {
        if (clr)
        {
            cfg.Clear();
        }

        string[] arr = block.CustomData.Split(new char[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < arr.Length; i++)
        {
            string ln = arr[i];
            string va;

            int p = ln.IndexOf('=');
            if (p > -1)
            {
                va = ln.Substring(p + 1);
                ln = ln.Substring(0, p);
            }
            else
            {
                va = "";
            }
            cfg[ln.Trim()] = va.Trim();
        }
    }

    public static void WriteCustomData(IMyTerminalBlock block, Dictionary<string, string> cfg)
    {
        StringBuilder sb = new StringBuilder(cfg.Count * 100);
        foreach (KeyValuePair<string, string> va in cfg)
        {
            sb.Append(va.Key).Append('=').Append(va.Value).Append('\n');
        }
        block.CustomData = sb.ToString();
    }
}