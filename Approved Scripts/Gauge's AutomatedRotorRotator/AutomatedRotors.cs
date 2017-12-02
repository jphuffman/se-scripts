/*


Automated Rotors V1.0.0

====================================================================================================
    SETUP
====================================================================================================

==== Simple Explanation ====

1. Have a rotor(s) on the same grid
2. Give the rotor(s) instructions in the programmable blocks "Custom Data" window
3. Run commands in the arguments textbox to control them

Example:
    # Add this to in the Custom Data field
    # Pound signs are comments

    increment loop Test Rotor
    reset 0 -30
    precision 0.01
    rotorlock true
    [30, 10, 2000]

You can activate your "Test Rotor" using the "run" command:

    run Test Rotor


==== Operations ====

simple - Takes a instruction set and executes it on command

    simple <rotor_name>

increment - Uses a single instruction and increments by that amount

    increment <mode> <rotor_name>
    <property> <value> <<value2>>
    [ <target_angle>, <rotation_speed>, <<wait_before_next>> ] <=== wait_before_next is only required when looping

automate - Executes many instructions one after another

    automate <mode> <rotor_name>
    <property> <value> <<value2>>
    [ <target_angle>, <rotation_speed>, <<wait_before_next>> ]

==== Modes ====

once            Performs all instructions then idles
loop            Performs all instructions and repeats

==== Properties ====

reset           Provides the angle and speed to reset to when the reset command is called
Default: 0 degrees 30 RPM

precision       The zone on ether side of the target_angle that is considered equal to it
Default: 0.01   (must be greater than 0)

rotorlock       If true, locks the rotor when idle
Default: false

==== Instruction Set ===
target_angle        The angle, in degrees, that will be stopped at (0 to 360)
rotation_speed      The desired rotation speed, in RPM (-30 to 30)
wait_before_next    The time, in milliseconds, a rotor should wait after completing a single 
		    instruction (0 to infinity)


==== Commandline Arguments ====

<command_name> <options>

Options: <rotor_name>, all

set         Execute a temperary instruction

    set [<target_angle>, <rotation_speed>] <rotor_name>
    set [<target_angle>, <rotation_speed>] all

run         Execute loaded instruction sets

    run <rotor_name>
    run all

suspend     Suspend execution
resume      Resume execution
reset       Execute the reset instruction set
kill        Set status to IDLE
*/

public enum Operations { automate, increment, simple };
public enum Properties { reset, precision, rotorlock }
public enum Modes { once, loop, none };
public enum RotorState { idle, active, resetting, waiting, suspended, single };

static int framesBeforeNextUpdate = 1;
static int offset = 3 * framesBeforeNextUpdate;

static StringBuilder runtimeMessages = new StringBuilder();
static Dictionary<string, AutomatedRotor> rotorDictionary = new Dictionary<string, AutomatedRotor>();
static List<IMyMotorStator> rotors = new List<IMyMotorStator>();

bool isInitialized = false;
bool isAlive = false;
int configSize = 0;

public void Main(string args)
{
    if (!isInitialized)
    {
	Runtime.UpdateFrequency = UpdateFrequency.Update10;
	framesBeforeNextUpdate = 10;
	isInitialized = true;
    }

    if (args.Length != 0)
    {
	isAlive = true;
	runtimeMessages.Clear();
	ConfigureRotors(true);
    }
    else
    {
	ConfigureRotors(false);
    }

    string command = args.Trim(' ', '\t', '\r');

    if (command.ToLower().StartsWith("run"))
    {
	command = command.Substring(3).Trim(' ', '\t');

	executeByOption(command, "start");
    }
    else if (command.ToLower().StartsWith("set"))
    {
	command = command.Substring(3).Trim(' ', '\t');

	executeByOption(command, "set");
    }
    else if (command.ToLower().StartsWith("reset"))
    {
	command = command.Substring(5).Trim(' ', '\t');

	executeByOption(command, "reset");
    }
    else if (command.ToLower().StartsWith("suspend"))
    {
	command = command.Substring(7).Trim(' ', '\t');

	executeByOption(command, "suspend");
    }
    else if (command.ToLower().StartsWith("resume"))
    {
	command = command.Substring(6).Trim(' ', '\t');

	executeByOption(command, "resume");
    }
    else if (command.ToLower().StartsWith("kill"))
    {
	command = command.Substring(4).Trim(' ', '\t');

	executeByOption(command, "kill");
    }

    Echo(runtimeMessages.ToString());
    if (isAlive)
    {
	foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
	{
	    bool isIncreasing = autoRotor.IsIncreasing();
	    float target = autoRotor.GetTargetAngle();
	    float angle = MathHelper.ToDegrees(autoRotor.Rotor.Angle);
	    float delta = RPM_to_Angle(autoRotor.Rotor.TargetVelocityRPM);
	    InstructionSet instruction = autoRotor.CurrentInstructionSet;


	    if (instruction != null && (autoRotor.State == RotorState.active || autoRotor.State == RotorState.resetting || autoRotor.State == RotorState.single))
	    {
		autoRotor.Rotor.TargetVelocityRPM = instruction.RotationSpeed;

		if (isIncreasing && target < angle)
		{
		    target += 360;
		}
		else if (!isIncreasing && angle < target)
		{
		    angle += 360;
		}

		if (Math.Abs(target - angle) < Math.Abs(delta * 2))
		{
		    if (Math.Abs(target - angle) < autoRotor.Precision)
		    {
			if (autoRotor.State == RotorState.resetting || autoRotor.State == RotorState.single)
			{
			    autoRotor.Kill();
			}
			else
			{
			    autoRotor.Wait();
			}
		    }
		    else
		    {
			//runtimeMessages.Append($"delta: {target - angle}\n");
			autoRotor.Rotor.TargetVelocityRPM = Angle_to_RPM((target - angle) / 2);
		    }
		}
	    }
	    else if (autoRotor.State == RotorState.waiting)
	    {
		if (autoRotor.HasTimeElapsed(Runtime.TimeSinceLastRun.Milliseconds))
		{
		    // logic for once/loop modes are built into the NextInstructionSet property
		    if (autoRotor.NextInstructionSet == null)
		    {
			autoRotor.Kill();
		    }
		    else
		    {
			autoRotor.ResetWaitTime();
			autoRotor.Resume();
		    }
		}
	    }

	    switch (autoRotor.State)
	    {
		case RotorState.idle:
		case RotorState.suspended:
		    Echo($"{autoRotor.Rotor.CustomName} | {autoRotor.State.ToString().ToUpper()} {angle.ToString("n2")}°");
		    break;
		case RotorState.active:
		case RotorState.resetting:
		case RotorState.single:
		    Echo($"{autoRotor.Rotor.CustomName} | {autoRotor.State.ToString().ToUpper()} {angle.ToString("n2")}°:{target.ToString("n2")}° {autoRotor.Rotor.TargetVelocityRPM.ToString("n2")}rpm");
		    break;
		case RotorState.waiting:
		    Echo($"{autoRotor.Rotor.CustomName} | {autoRotor.State.ToString().ToUpper()} {instruction.WaitBeforeNext - autoRotor.CurrentWaitedTime} {angle.ToString("n2")}°");
		    break;
	    }

	    autoRotor.LastAngle = angle;
	}
    }
}

public void ConfigureRotors(bool shouldBeAlive)
{
    // Only update rotor instructions when config file has been changed
    if (Me.CustomData.Length == configSize)
    {
	return;
    }

    // Ensure the rotor list contains all the current rotors
    rotors.Clear();
    GridTerminalSystem.GetBlocksOfType(rotors);
    configSize = Me.CustomData.Length;

    // stop all active rotors before update

    isAlive = shouldBeAlive;
    runtimeMessages.Clear();
    foreach (AutomatedRotor r in rotorDictionary.Values)
    {
	r.Kill();
    }

    AutomatedRotor autoRotor = null;
    string[] lines = Me.CustomData.Split(new char[] { '\n', '\r' });
    string[] operations = Enum.GetNames(typeof(Operations));
    string[] modes = Enum.GetNames(typeof(Modes));
    string[] properties = Enum.GetNames(typeof(Properties));

    for (int i = 0; i < lines.Length; i++)
    {
	string line = lines[i].Trim(' ', '\t');
	string[] settings = line.Split(' ').Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("\t")).ToArray();
	string firstCommand = (settings.Length > 0) ? settings[0].ToLower() : "";

	if (operations.Contains(firstCommand))
	{
	    // save and clear for next rotor
	    if (autoRotor != null)
	    {
		AddRotor(autoRotor);
		autoRotor = null;
	    }

	    Operations operation = (Operations)Enum.Parse(typeof(Operations), firstCommand);

	    if (settings.Length < 3 || operation == Operations.simple && settings.Length < 2)
	    {
		runtimeMessages.Append($"[ERROR] Line {i + 1}: Not enough arguments\n");
	    }
	    else
	    {
		Modes mode = Modes.none;
		string rotorName = string.Empty;
		if (operation == Operations.simple)
		{
		    rotorName = line.Substring(settings[0].Length).TrimStart(' ', '\t').ToLower();
		}
		else if (modes.Contains(settings[1].ToLower()))
		{
		    mode = (Modes)Enum.Parse(typeof(Modes), settings[1].ToLower());

		    rotorName = line.Substring(settings[0].Length).TrimStart(' ', '\t');
		    rotorName = rotorName.Substring(settings[1].Length).TrimStart(' ', '\t').ToLower();
		}
		else
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: The mode given does not mach existing modes\n");
		}

		IMyMotorStator rotor = rotors.Find(r => r.CustomName.ToLower().Contains(rotorName));
		if (rotor == null)
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Could not find rotor with name \"{rotorName}\"\n");
		}
		else
		{
		    autoRotor = new AutomatedRotor(rotor, operation, mode, new List<InstructionSet>());
		}
	    }
	}
	else if (properties.Contains(firstCommand))
	{
	    Properties property = (Properties)Enum.Parse(typeof(Properties), firstCommand);

	    if (autoRotor == null || autoRotor.Operation == Operations.simple)
	    {
		runtimeMessages.Append($"[WARNING] Line {i + 1}: Skipping {settings[0]}\n");
	    }
	    else if (settings.Length < 2)
	    {
		runtimeMessages.Append($"[WARNING] Line {i + 1}: {settings[0]} does not have a value\n");
	    }
	    else if (property == Properties.reset)
	    {
		if (settings.Length < 3)
		{
		    runtimeMessages.Append($"[WARNING] Line {i + 1}: Reset property does not have a speed value\n");
		}
		else
		{
		    float angle;
		    if (float.TryParse(settings[1], out angle))
		    {
			float speed;
			if (float.TryParse(settings[2], out speed))
			{
			    autoRotor.ResetInstructions = new InstructionSet(speed, angle, 0);
			}
			else
			{
			    runtimeMessages.Append($"[WARNING] Line {i + 1}: Failed to parse the Reset property speed value.\n");
			}
		    }
		    else
		    {
			runtimeMessages.Append($"[WARNING] Line {i + 1}: Failed to parse the Reset property angle value.\n");
		    }
		}
	    }
	    else if (property == Properties.rotorlock)
	    {
		bool shouldRotorLock;
		if (bool.TryParse(settings[1], out shouldRotorLock))
		{
		    autoRotor.LockRotor = shouldRotorLock;
		}
		else
		{
		    runtimeMessages.Append($"[WARNING] Line {i + 1}: Failed to parse RotorLock value.\n");
		}
	    }
	    else if (property == Properties.precision)
	    {
		float value;
		if (float.TryParse(settings[1], out value))
		{
		    if (value > 0)
		    {
			autoRotor.Precision = value;
		    }
		    else
		    {
			runtimeMessages.Append($"[WARNING] Line {i + 1}: Precision value must be greater than 0\n");
		    }
		}
		else
		{
		    runtimeMessages.Append($"[WARNING] Line {i + 1}: Failed to parse Precision value.\n");
		}
	    }
	    else
	    {
		runtimeMessages.Append($"[WARNING] {settings[0]} is not being properly handled please contact the script owner\n");
	    }
	}
	else if (line.StartsWith("["))
	{
	    if (autoRotor == null || autoRotor.Operation == Operations.simple)
	    {
		runtimeMessages.Append($"[ERROR] Line {i + 1}: Cannot add instruction set\n");
	    }
	    else
	    {
		string[] instructionSet = line.Trim(new char[] { '[', ']' }).Split(',');

		float angle;
		float speed;
		long time = 0;
		if (instructionSet.Length < 3 && !(autoRotor.Operation == Operations.increment && autoRotor.Mode == Modes.once))
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Instruction set must contain waitTime\n");
		}
		else if (instructionSet.Length < 2 && autoRotor.Operation == Operations.increment && autoRotor.Mode == Modes.once)
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Instruction set is malformed\n");
		}
		else if (autoRotor.Operation == Operations.increment && autoRotor.Instructions.Count == 1)
		{
		    runtimeMessages.Append($"[WARNING] Line {i + 1}: Ignoring extra instruction set\n");
		}
		else if (!float.TryParse(instructionSet[0], out angle))
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Failed to parse angle\n");
		}
		else if (!float.TryParse(instructionSet[1], out speed))
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Failed to parse speed\n");
		}
		else if (instructionSet.Length > 2 && !long.TryParse(instructionSet[2], out time))
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Failed to parse time\n");
		}
		else if (speed == 0)
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: Speed must not be 0\n");
		}
		else if (time < 0)
		{
		    runtimeMessages.Append($"[ERROR] Line {i + 1}: WiatTime must be greater than 0\n");
		}
		else
		{
		    autoRotor.Instructions.Add(new InstructionSet(speed, angle, time));
		}
	    }
	}
	else if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
	{
	    // this lets people add comments
	}
	else
	{
	    runtimeMessages.Append($"[WARNING] Line {i + 1}: Failed to parse {settings[0]}, check spelling\n");
	}
    }

    // Adds the last rotor to the dictionary
    AddRotor(autoRotor);

    runtimeMessages.Append($"Done. {rotorDictionary.Count} rotors parsed\n");
}

private void AddRotor(AutomatedRotor autoRotor)
{
    if (autoRotor != null)
    {
	if (!rotorDictionary.ContainsKey(autoRotor.Rotor.CustomName.ToLower()))
	{
	    rotorDictionary.Add(autoRotor.Rotor.CustomName.ToLower(), autoRotor);
	}
	else
	{
	    rotorDictionary[autoRotor.Rotor.CustomName.ToLower()] = autoRotor;
	}
    }
}

public static void executeByOption(string command, string functionName)
{
    InstructionSet set = null;
    if (command.StartsWith("["))
    {
	string[] instructionSet = command.Substring(0, command.IndexOf(']')).Trim(new char[] { '[', ']' }).Split(',');

	float angle;
	float speed;
	if (instructionSet.Length < 2)
	{
	    runtimeMessages.Append($"[ERROR] Instruction set is malformed\n");
	}
	else if (!float.TryParse(instructionSet[0], out angle))
	{
	    runtimeMessages.Append($"[ERROR] Failed to parse angle\n");
	}
	else if (!float.TryParse(instructionSet[1], out speed))
	{
	    runtimeMessages.Append($"[ERROR] Failed to parse speed\n");
	}
	else if (speed == 0)
	{
	    runtimeMessages.Append($"[ERROR] Speed must not be 0\n");
	}
	else
	{
	    set = new InstructionSet(speed, angle, 0);
	    command = command.Substring(command.IndexOf(']') + 1).Trim(' ', '\t');
	}
    }

    if (command.ToLower() == "all")
    {
	foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
	{
	    autoRotor.RunCommand(functionName, set);
	}
    }
    else if (rotorDictionary.ContainsKey(command.ToLower()))
    {
	rotorDictionary[command.ToLower()].RunCommand(functionName, set);
    }
    else
    {
	runtimeMessages.Append($"[ERROR] Unrecognized command {command}\n");
    }
}

public static float RPM_to_Angle(float value)
{
    return (value / 60f / 60f * 360f * framesBeforeNextUpdate);
}

public static float Angle_to_RPM(float value)
{
    return (value * 60f * 60f / 360f / framesBeforeNextUpdate);
}

public static float CircleSubtract(float baseValue, float subtractValue, bool isGain)
{
    if (isGain && baseValue <= offset && subtractValue >= 360 - offset)
    {
	return (360 + baseValue) - subtractValue;
    }

    if (!isGain && baseValue >= 360 - offset && subtractValue <= offset)
    {
	return baseValue - (360 + subtractValue);
    }

    return baseValue - subtractValue;
}

public static float AdjustAngle(float angle)
{
    float correctionAmount = 360 * (float)Math.Abs(Math.Floor(angle / 360));

    if (angle > 360)
    {
	angle -= correctionAmount;
    }
    else if (angle < 0)
    {
	angle += correctionAmount;
    }

    return angle;
}

public class InstructionSet
{
    public float RotationSpeed { get; }
    public float TargetAngle { get; }
    public long WaitBeforeNext { get; }

    public InstructionSet(float s, float a, long w)
    {
	RotationSpeed = MathHelper.Clamp(s, -30, 30);
	TargetAngle = AdjustAngle(a);
	WaitBeforeNext = w;
    }

    public override string ToString()
    {
	return $"Speed: {RotationSpeed}, Stop Angle: {TargetAngle}, Wait: {WaitBeforeNext}";
    }
}

public class AutomatedRotor
{
    private int currentInstructionIndex = -1;
    private long currentTimeWaited = 0;

    public IMyMotorStator Rotor { get; set; }
    public Operations Operation { get; set; }
    public Modes Mode { get; set; }
    public RotorState State { get; private set; }

    public long CurrentWaitedTime { get { return currentTimeWaited; } }
    public float LastAngle { get; set; }
    public float IncrementAngle { get; private set; }
    public bool LockRotor { get; set; }
    public float Precision { get; set; }

    public List<InstructionSet> Instructions { get; }
    public InstructionSet ResetInstructions { get; set; }
    public InstructionSet TempInstructions { get; set; }

    public InstructionSet CurrentInstructionSet
    {
	get
	{
	    if (State == RotorState.resetting)
	    {
		return ResetInstructions;
	    }

	    if (State == RotorState.single)
	    {
		return TempInstructions;
	    }

	    if (State == RotorState.idle)
	    {
		return null;
	    }

	    if (Instructions.Count == 0)
	    {
		return null;
	    }

	    return Instructions[currentInstructionIndex];
	}
    }

    public InstructionSet NextInstructionSet
    {
	get
	{
	    if (Instructions.Count == 0)
	    {
		return null;
	    }

	    currentInstructionIndex++;
	    if (currentInstructionIndex >= Instructions.Count)
	    {
		if (Mode == Modes.loop)
		{
		    currentInstructionIndex = 0;
		}
		else
		{
		    return null;
		}
	    }

	    return Instructions[currentInstructionIndex];
	}
    }

    public AutomatedRotor(IMyMotorStator r, Operations o, Modes m, List<InstructionSet> i)
    {
	Rotor = r;
	Rotor.BrakingTorque = 33600000;
	LockRotor = false;
	Precision = 0.01f;
	Rotor.SetValue("RotorLock", LockRotor);

	Operation = o;
	Mode = m;
	State = RotorState.idle;
	Instructions = i;
	IncrementAngle = MathHelper.ToDegrees(r.Angle);
	ResetInstructions = new InstructionSet(1, MathHelper.ToDegrees(r.Angle), 0);
    }

    private void internalReset()
    {
	Rotor.SetValue("RotorLock", LockRotor);
	Rotor.TargetVelocityRPM = 0;
	currentInstructionIndex = 0;
	currentTimeWaited = 0;
	TempInstructions = null;
    }

    public void RunCommand(string command, InstructionSet set)
    {
	switch (command)
	{
	    case "start":
		Start();
		break;

	    case "set":
		Set(set);
		break;

	    case "reset":
		Reset();
		break;

	    case "suspend":
		Suspend();
		break;

	    case "resume":
		Resume();
		break;

	    case "kill":
		Kill();
		break;

	    case "wait":
		Wait();
		break;
	}
    }

    public void Start()
    {
	if (State == RotorState.idle || State == RotorState.waiting)
	{
	    internalReset();
	    Rotor.SetValue("RotorLock", false);
	    State = RotorState.active;

	    if (Operation == Operations.increment && Instructions.Count != 0)
	    {
		IncrementAngle = AdjustAngle(Instructions[0].RotationSpeed > 0 ? IncrementAngle + Instructions[0].TargetAngle : IncrementAngle - Instructions[0].TargetAngle);
	    }
	}
    }

    public void Set(InstructionSet instruction)
    {
	if (instruction != null)
	{
	    TempInstructions = instruction;
	    Rotor.SetValue("RotorLock", false);
	    State = RotorState.single;
	}
    }

    public void Reset()
    {
	internalReset();
	Rotor.SetValue("RotorLock", false);
	IncrementAngle = ResetInstructions.TargetAngle;
	State = RotorState.resetting;
    }

    public void Suspend()
    {
	if (State != RotorState.idle)
	{
	    Rotor.SetValue("RotorLock", LockRotor);
	    Rotor.TargetVelocityRad = 0;
	    State = RotorState.suspended;
	}
    }

    public void Resume()
    {
	if (State == RotorState.suspended || State == RotorState.waiting)
	{
	    Rotor.SetValue("RotorLock", false);
	    State = RotorState.active;
	}
    }

    public void Kill()
    {
	internalReset();
	State = RotorState.idle;
    }

    public void Wait()
    {
	Rotor.TargetVelocityRPM = 0;
	Rotor.SetValue("RotorLock", LockRotor);
	State = RotorState.waiting;
    }

    public bool IsIncreasing()
    {
	return (CurrentInstructionSet == null) ? true : CurrentInstructionSet.RotationSpeed > 0;
    }

    public float GetTargetAngle()
    {
	float angle = 0;
	if (State == RotorState.resetting)
	{
	    angle = ResetInstructions.TargetAngle;
	}
	else if (Operation == Operations.automate)
	{
	    if (CurrentInstructionSet != null)
	    {
		angle = CurrentInstructionSet.TargetAngle;
	    }
	}
	else if (Operation == Operations.increment)
	{
	    angle = IncrementAngle;
	}

	return angle;
    }

    public void ResetWaitTime()
    {
	currentTimeWaited = 0;
    }

    public bool HasTimeElapsed(long mil)
    {
	if (CurrentInstructionSet == null)
	{
	    return false;
	}
	else
	{
	    currentTimeWaited += mil;
	    return currentTimeWaited > CurrentInstructionSet.WaitBeforeNext;
	}
    }

    public override string ToString()
    {
	return $"Rotor: {Rotor.CustomName}\n{Operation}:{Mode}\n{State.ToString().ToUpper()}\nInstructions: {Instructions.Count()}\nCurrent: {MathHelper.ToDegrees(Rotor.Angle)}\nLast {LastAngle}\nIncrement By {IncrementAngle}";
    }
}
