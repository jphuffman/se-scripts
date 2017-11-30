/*
Automated Rotors V0.0.1
Set rotor instructions in this programmable blocks "Custom Data" window

rotor_name			(full/partial - takes the first match)
speed 				(in RPMs)
stop_angle 			(in degrees)
wait_before_next 	(in milliseconds)

Operations: Automate, Increment
Properties: StartAngle
Modes: Once, Loop

Format:

automate <mode> <rotor_name>
<property> <value>
[<stop_angle>, <speed>, <wait_before_next>]

increment <mode> <rotor_name>
[<stop_angle>, <speed>, <wait_before_next>]

automate once Rotor 1
StartAngle 35
[90, -15, 3000]
[4, 30, 1000]
[359, 1, 2000]

automate loop Rotor 2
startangle 0
[154, 15, 8000]
[300, 20, 4000]
[60, -3, 1000]

increment once Incremental Rotor
[50, 13]

increment loop Incremental Rotor 2
[5, 2, 5000]

Commands:
options: all, rotor_name

verify				(tests all rotors to ensure they exist and are not damaged)
start <option>		(executes rotor(s) instruction set)
reset <option>
suspend <option>	(suspends current task)
resume <option>		(resumes current task)
kill <option>		(clears current task)
*/

public enum Operations { automate, increment };
public enum Properties { reset }
public enum Modes { once, loop };
public enum RotorState { idle, active, resetting, waiting, suspended };

public class InstructionSet
{
	public float RotationSpeed { get; }
	public float TargetAngle { get; }
	public long WaitBeforeNext { get; }

	public InstructionSet(float s, float a, long w)
	{
		RotationSpeed = s;
		TargetAngle = a;
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
	public List<InstructionSet> Instructions { get; }
	public InstructionSet ResetInstructions { get; set; }

	public InstructionSet CurrentInstructionSet
	{
		get
		{
			if (State == RotorState.resetting)
			{
				return ResetInstructions;
			}

			if (Instructions.Count == 0)
			{
				return null;
			}

			if (currentInstructionIndex == -1)
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
		Rotor.BrakingTorque = 50f;

		Operation = o;
		Mode = m;
		State = RotorState.idle;
		Instructions = i;
		IncrementAngle = ToDegrees(r.Angle);
		ResetInstructions = new InstructionSet(1, ToDegrees(r.Angle), 0);
	}

	private void internalReset()
	{
		Rotor.TargetVelocityRPM = 0;
		currentInstructionIndex = -1;
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

	public void Start()
	{
		if (State == RotorState.idle)
		{
			internalReset();
			currentInstructionIndex = 0;
			State = RotorState.active;

			if (Operation == Operations.increment)
			{
				IncrementAngle = AdjustAngle(Instructions[0].RotationSpeed > 0 ? IncrementAngle + Instructions[0].TargetAngle : IncrementAngle - Instructions[0].TargetAngle);
			}
		}
	}

	public void Reset()
	{
		internalReset();
		IncrementAngle = ResetInstructions.TargetAngle;
		State = RotorState.resetting;
	}

	public void Suspend()
	{
		if (State != RotorState.idle)
		{
			Rotor.TargetVelocityRad = 0;
			State = RotorState.suspended;
		}
	}

	public void Resume()
	{
		State = RotorState.active;
	}

	public void Kill()
	{
		internalReset();
		State = RotorState.idle;
	}

	public void Wait()
	{
		Rotor.TargetVelocityRPM = 0;
		State = RotorState.waiting;
	}

	public bool IsGaining()
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

	public override string ToString()
	{
		return $"Rotor: {Rotor.CustomName}\n{Operation}:{Mode}\n{State.ToString().ToUpper()}\nInstructions: {Instructions.Count()}\nCurrent: {ToDegrees(Rotor.Angle)}\nLast {LastAngle}\nIncrement By {IncrementAngle}";
	}
}

static int framesBeforeNextUpdate = 1;
static int offset = 3 * framesBeforeNextUpdate;

bool isInitialized = false;
bool isAlive = false;
int configSize = 0;
StringBuilder runtimeMessages = new StringBuilder();
Dictionary<string, AutomatedRotor> rotorDictionary = new Dictionary<string, AutomatedRotor>();
List<IMyMotorStator> rotors = new List<IMyMotorStator>();

public void Main(string args)
{
	if (!isInitialized)
	{
		Runtime.UpdateFrequency = UpdateFrequency.Update1;
		framesBeforeNextUpdate = 1;
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
		command = command.Substring(3).Trim(' ');

		if (command.ToLower() == "all")
		{
			foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
			{
				autoRotor.Start();
			}
		}
		else if (rotorDictionary.ContainsKey(command))
		{
			if (rotorDictionary[command].State == RotorState.idle)
			{
				rotorDictionary[command].Start();
			}
		}
		else
		{
			runtimeMessages.Append("[ERROR] The \"run\" command requires the keyword \"all\" or a rotor name\n");
		}
	}
	else if (command.ToLower().StartsWith("reset"))
	{
		command = command.Substring(5).Trim(' ');

		if (command.ToLower() == "all")
		{
			foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
			{
				autoRotor.Reset();
			}
		}
		else if (rotorDictionary.ContainsKey(command))
		{
			rotorDictionary[command].Reset();
		}
		else
		{
			runtimeMessages.Append("[ERROR] The \"reset\" command requires the keyword \"all\" or a rotor name\n");
		}
	}
	else if (command.ToLower().StartsWith("suspend"))
	{
		command = command.Substring(7).Trim(' ');

		if (command.ToLower() == "all")
		{
			foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
			{
				if (autoRotor.State != RotorState.idle)
				{
					autoRotor.Suspend();
				}
			}
		}
		else if (rotorDictionary.ContainsKey(command))
		{
			if (rotorDictionary[command].State != RotorState.suspended)
			{
				rotorDictionary[command].Suspend();
			}
		}
		else
		{
			runtimeMessages.Append("[ERROR] The \"suspend\" command requires the keyword \"all\" or a rotor name\n");
		}
	}
	else if (command.ToLower().StartsWith("resume"))
	{
		command = command.Substring(6).Trim(' ');

		if (command.ToLower() == "all")
		{
			foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
			{
				if (autoRotor.State == RotorState.suspended)
				{
					autoRotor.Resume();
				}
			}
		}
		else if (rotorDictionary.ContainsKey(command))
		{
			if (rotorDictionary[command].State == RotorState.suspended)
			{
				rotorDictionary[command].Resume();
			}
		}
		else
		{
			runtimeMessages.Append("[ERROR] The \"resume\" command requires the keyword \"all\" or a rotor name\n");
		}
	}
	else if (command.ToLower().StartsWith("kill"))
	{
		command = command.Substring(4).Trim(' ');

		if (command.ToLower() == "all")
		{
			foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
			{
				autoRotor.Kill();
			}
		}
		else if (rotorDictionary.ContainsKey(command))
		{
			rotorDictionary[command].Kill();
		}
		else
		{
			runtimeMessages.Append("[ERROR] The \"kill\" command requires the keyword \"all\" or a rotor name\n");
		}
	}

	Echo(runtimeMessages.ToString());
	if (isAlive)
	{
		foreach (AutomatedRotor autoRotor in rotorDictionary.Values)
		{
			bool isGain = autoRotor.IsGaining();
			float targetAngle = autoRotor.GetTargetAngle();
			float rotorAngle = AdjustAngle(ToDegrees(autoRotor.Rotor.Angle));
			float rotorAngleDelta = RPM_to_Angle(autoRotor.Rotor.TargetVelocityRPM);
			InstructionSet instruction = autoRotor.CurrentInstructionSet;

			if ((autoRotor.State == RotorState.active || autoRotor.State == RotorState.resetting) && instruction != null)
			{
				autoRotor.Rotor.TargetVelocityRPM = instruction.RotationSpeed; 
				float desiredDelta = CircleSubtract(targetAngle, rotorAngle, isGain);
				float desiredRPM = Angle_to_RPM(desiredDelta);

				if (desiredRPM < instruction.RotationSpeed)
				{
					runtimeMessages.Append($"{desiredDelta} {desiredRPM}\n");
				}

				if (desiredRPM > 0 && instruction.RotationSpeed > 0 || desiredRPM < 0 && instruction.RotationSpeed < 0)
				{
					autoRotor.Rotor.TargetVelocityRPM = (desiredRPM > instruction.RotationSpeed) ? instruction.RotationSpeed : desiredRPM;
				}
				
				float margin = RPM_to_Angle(instruction.RotationSpeed);
				if (Math.Abs(CircleSubtract(targetAngle, rotorAngle, isGain)) < margin)
				{
					if (autoRotor.State == RotorState.resetting)
					{
						autoRotor.Kill();
					}
					else
					{
						autoRotor.Wait();
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
					Echo($"{autoRotor.Rotor.CustomName} | {autoRotor.State.ToString().ToUpper()} {rotorAngle.ToString("n2")}°");
					break;
				case RotorState.active:
				case RotorState.resetting:
					Echo($"{autoRotor.Rotor.CustomName} | {autoRotor.State.ToString().ToUpper()} {rotorAngle.ToString("n2")}°:{targetAngle.ToString("n2")}° {autoRotor.Rotor.TargetVelocityRPM.ToString("n2")}rpm");
					break;
				case RotorState.waiting:
					Echo($"{autoRotor.Rotor.CustomName} | {autoRotor.State.ToString().ToUpper()} {instruction.WaitBeforeNext - autoRotor.CurrentWaitedTime} {rotorAngle.ToString("n2")}°");
					break;
			}

			autoRotor.LastAngle = rotorAngle;
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
		string[] settings = line.Split(' ');
		string firstCommand = settings[0].ToLower();

		if (operations.Contains(firstCommand))
		{
			// save and clear for next rotor
			if (autoRotor != null)
			{
				AddRotor(autoRotor);
				autoRotor = null;
			}

			Operations operation = (Operations)Enum.Parse(typeof(Operations), firstCommand);

			if (settings.Length < 3)
			{
				runtimeMessages.Append($"[ERROR] Line {i + 1}: Not enough arguments\n");
			}
			else
			{
				if (!modes.Contains(settings[1].ToLower()))
				{
					runtimeMessages.Append($"[ERROR] Line {i + 1}: The mode given does not mach existing modes\n");
				}
				else
				{
					Modes mode = (Modes)Enum.Parse(typeof(Modes), settings[1].ToLower());

					string rotorName = line.Substring(settings[0].Length + settings[1].Length + 2).TrimEnd(' ').ToLower();
					IMyMotorStator rotor = rotors.Find(r => r.CustomName.ToLower().Contains(rotorName));

					if (rotor == null)
					{
						runtimeMessages.Append($"[ERROR] Line {i + 1}: Could not find rotor with name {rotorName}\n");
					}
					else
					{
						autoRotor = new AutomatedRotor(rotor, operation, mode, new List<InstructionSet>());
					}
				}
			}
		}
		else if (properties.Contains(firstCommand))
		{
			Properties property = (Properties)Enum.Parse(typeof(Properties), firstCommand);

			if (autoRotor == null)
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
							autoRotor.ResetInstructions = new InstructionSet(speed, AdjustAngle(angle), 0);
						}
						else
						{
							runtimeMessages.Append($"[WARNING] Line {i + 1}: Failed to parse the Reset property speed value. Using Default\n");
						}
					}
					else
					{
						runtimeMessages.Append($"[WARNING] Line {i+1}: Failed to parse the Reset property angle value. Using Default\n");
					}
				}
			}
			else
			{
				runtimeMessages.Append($"[WARNING] {settings[0]} is not being properly handled please contact the script owner\n");
			}
		}
		else if (line.StartsWith("["))
		{
			if (autoRotor == null)
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
					runtimeMessages.Append($"[ERROR] Line {i + 1}: Failed to parse speed\n");
				}
				else if (speed == 0)
				{
					runtimeMessages.Append($"[ERROR] Line {i + 1}: Speed must not be 0\n");
				}
				else
				{
					angle = AdjustAngle(angle);

					if (angle == 0 || angle == 360)
					{
						angle = (speed > 0) ? 360 : 0;
					}

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
		if (!rotorDictionary.ContainsKey(autoRotor.Rotor.CustomName))
		{
			rotorDictionary.Add(autoRotor.Rotor.CustomName, autoRotor);
		}
		else
		{
			rotorDictionary[autoRotor.Rotor.CustomName] = autoRotor;
		}
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
	if (isGain && baseValue <= offset && subtractValue >= 360-offset)
	{
		return (360 + baseValue) - subtractValue;
	}

	if (!isGain && baseValue >= 360-offset && subtractValue <= offset)
	{
		return baseValue - (360 + subtractValue);
	}

	return baseValue - subtractValue;
}

public static float ToDegrees(float radians)
{
	return (float)(radians * 57.2958);
}

public static float AdjustAngle(float angle)
{
	if (angle > 360)
	{
		angle -= 360;
		return AdjustAngle(angle);
	}
	else if (angle < 0)
	{
		angle += 360;
		return AdjustAngle(angle);
	}

	return angle;
}