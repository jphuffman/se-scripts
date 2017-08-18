//------------------------------------------------------------
// ADN - Easy Lidar Homing Script v8.0
//------------------------------------------------------------

//----- Refer To Steam Workshop Discussion Section For Variables Definition -----

//Sets the default missile homing mode. See http://steamcommunity.com/workshop/filedetails/discussion/807454034/1457328392110577137/ for more information.
int missileLaunchType = 0;

//Type of block to disconnect missile from launching ship: 0 = Merge Block, 1 = Rotor, 2 = Connector, 3 = Merge Block And Any Locked Connectors, 4 = Rotor And Any Locked Connectors, 99 = No detach required
int missileDetachPortType = 0;

//Spin Missile By This RPM After Launch Clearance
int spinAmount = 0;

//Whether to perform a vertical takeoff for the launching procedure
bool verticalTakeoff = false;

//Whether to fly straight first until LOCK command is given via R_TARGET Text Panel to start homing. Activating this will ignore launchSeconds variable
bool waitForHomingTrigger = false;

//Whether to allow missile to read Custom Data of the R_TARGET to get command during missile homing
bool enableMissileCommand = true;

//Script will only extract missile blocks on same grid as this PB
bool missileBlockSameGridOnly = true;

//The amount of spread for 5 point raycast for easier initial lock-on. Zero to use normal single beam lock-on
float fivePointInitialLockDist = 0f;

//------------------------------ Inter Grid Communications Configuration ------------------------------

string missileId = null;
string missileGroup = null;
string allowedSenderId = null;

//------------------------------ Reference Block Name Configuration ------------------------------

string strShipRefLidar = "R_LIDAR";
string strShipRefFwd = "R_FORWARD";
string strShipRefPanel = "R_TARGET";

string strShipRefNotMissileTag = "NOT_MISSILE";

string missileActivationCommands = "";

string missileTriggerCommands = "";

string proximityTriggerCommands = "SETWB:Warhead:Safety:True,ACTW:Warhead:Detonate";

string failunsafeTriggerCommands = "SETWB:Warhead:Safety:True";

string strGyroscopesTag = "";
string strThrustersTag = "";
string strDetachPortTag = "";
string strDirectionRefBlockTag = "";

string strProximitySensorTag = "PROXIMITY";

string strLockTriggerBlockTag = "R_ALERT";
string strLockTriggerAction = "PlaySound";

string strStatusDisplayPrefix = "<D>";

//------------------------------ Lidar Lock On Configuration ------------------------------

float LIDAR_MIN_LOCK_DISTANCE = 50;
float LIDAR_MAX_LOCK_DISTANCE = 3000;

int LIDAR_REFRESH_INTERVAL = 0;

float LIDAR_REFRESH_CALC_FACTOR = 0.85f;

bool excludeFriendly = false;

//------------------------------ Missile Handling Configuration ------------------------------

double driftVectorReduction = 1.2;
double launchSeconds = 1;

bool? boolDrift = null;
bool? boolLeadTarget = null;
bool? boolNaturalDampener = null;

//------------------------------ Above Is User Configuration Section. This Section Is For PID Tuning ------------------------------

double DEF_SMALL_GRID_P = 300;
double DEF_SMALL_GRID_I = 0.1;
double DEF_SMALL_GRID_D = 100;

double DEF_BIG_GRID_P = 50;
double DEF_BIG_GRID_I = 0.5;
double DEF_BIG_GRID_D = 4;

bool useDefaultPIDValues = true;

double AIM_P = 0;
double AIM_I = 0;
double AIM_D = 0;
double AIM_LIMIT = 60;

double INTEGRAL_WINDUP_LIMIT = 0;

//------------------------------ Script Parameters Configuration ------------------------------

int MERGE_SEPARATE_WAIT_THRESHOLD = 60;

double TURRET_AI_PN_CONSTANT = 5;
int TURRET_AI_AVERAGE_SIZE = 1;

bool outputMissileStatus = true;

//------------------------------ Below Is Main Script Body ------------------------------

List<IMyCameraBlock> shipRefLidars = null;
IMyTerminalBlock shipRefFwd = null;
IMyTextPanel shipRefPanel = null;

List<IMyCameraBlock> missileLidars = null;
IMyShipController remoteControl = null;
IMyTerminalBlock refFwdBlock = null;
IMyTerminalBlock refDwdBlock = null;

bool hasProximitySensors = false;
List<ProximitySensor> proximitySensors = null;
int failunsafeGrpCnt = 0;

bool isLidarMode = false;

IMyLargeTurretBase homingTurret = null;
VectorAverageFilter turretVectorFilter = null;

IMyTerminalBlock alertBlock = null;
IMyTerminalBlock statusDisplay = null;

IMyTerminalBlock notMissile = null;
double notMissileRadius = 0;

bool homingReleaseLock = false;

bool commsPositionSet = false;
Vector3D commsPosition = default(Vector3D);

bool commsFwdSet = false;
RayD commsFwd = default(RayD);

bool commsLidarTargetSet = false;
MyDetectedEntityInfo commsLidarTarget = default(MyDetectedEntityInfo);

List<IMyTerminalBlock> gyros = null;
string[] gyroYawField = null;
string[] gyroPitchField = null;
string[] gyroRollField = null;
float[] gyroYawFactor = null;
float[] gyroPitchFactor = null;
float[] gyroRollFactor = null;

List<IMyTerminalBlock> thrusters = null;
float[] thrustValues = null;

List<IMyTerminalBlock> launchThrusters = null;

MatrixD refWorldMatrix = default(MatrixD);
MatrixD refViewMatrix = default(MatrixD);
bool refFwdReverse = false;

bool fwdIsTurret = false;

Vector3D refFwdPosition = new Vector3D();
Vector3D refFwdVector = new Vector3D();
bool refFwdSet = false;

Vector3D naturalGravity = new Vector3D();
double naturalGravityLength = 0;

Vector3D midPoint = new Vector3D();
Vector3D driftVector = new Vector3D();
double speed = 0;
double rpm = 0;

Vector3D lastMidPoint = new Vector3D();
Vector3D lastNormal = new Vector3D();

MyDetectedEntityInfo lidarTargetInfo = default(MyDetectedEntityInfo);

Vector3D targetPosition = new Vector3D();
Vector3D lastTargetPosition = new Vector3D();
Vector3D offsetTargetPosition = new Vector3D();

bool targetPositionSet = false;
long lastTargetPositionClock = 0;

Vector3D targetVector = new Vector3D();
double distToTarget = 0;

Vector3D targetDirection = new Vector3D();
double targetSpeed = 0;
double targetRadius = 0;

double targetYawAngle = 0;
double targetPitchAngle = 0;
double targetRollAngle = 0;

double lastYawError = 0;
double lastYawIntegral = 0;
double lastPitchError = 0;
double lastPitchIntegral = 0;
double lastRollError = 0;
double lastRollIntegral = 0;

long subCounter = 0;
int subMode = 0;
int mode = -2;
long delta = 0;
long clock = 0;
bool init = false;

IMyTerminalBlock detachBlock = null;
int detachBlockType = -1;

List<KeyValuePair<double, string[]>> rpmTriggerList = null;
List<KeyValuePair<double, string[]>> distTriggerList = null;
List<KeyValuePair<long, string[]>> timeTriggerList = null;
bool haveTriggerCommands = false;

Random rnd = new Random();

float coneLimit = 0;
double sideScale = 0;
double ticksRatio = 0;
double ticksFactor = 0;

int lidarStaggerIndex = 0;

long nextLidarTriggerTicks = 0;
long nextLidarRecountTicks = 0;

const double RPM_FACTOR = 1800 / Math.PI;
const double ACOS_FACTOR = 180 / Math.PI;
const float GYRO_FACTOR = (float)(Math.PI / 30);
const double RADIAN_FACTOR = Math.PI / 180;

const long MIN_RECOUNT_TICKS = 2500000;
const long MIN_RUN_TICKS = 100000;
const float SECOND = 10000000f;

Vector3D Y_VECTOR = new Vector3D(0, -1, 0);
Vector3D Z_VECTOR = new Vector3D(0, 0, -1);
Vector3D POINT_ZERO = new Vector3D(0, 0, 0);

void Main(string arguments)
{
	//---------- Initialization And General Controls ----------

	if (!init)
	{
		if (subMode == 0)
		{
			subMode = 1;

			missileId = Me.GetId().ToString();
			missileGroup = null;

			if (Me.CustomData.Length > 0)
			{
				ProcessCustomConfiguration();
			}

			if (arguments != null && arguments.Length > 0)
			{
				ProcessConfigurationCommand(arguments);
				return;
			}
		}

		if (subMode == 1)
		{
			InitLaunchingShipRefBlocks();

			if (shipRefFwd != null)
			{
				refFwdPosition = shipRefFwd.WorldMatrix.Translation;
				refFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefFwd.WorldMatrix.Forward);
			}

			if (!DetachFromGrid())
			{
				throw new Exception("--- Initialization Failed ---");
			}

			subCounter = 0;
			subMode = (missileDetachPortType == 99 ? 3 : 2);
			return;
		}
		else if (subMode == 2)
		{
			bool isDetached = false;

			if (notMissile != null)
			{
				isDetached = (notMissile.CubeGrid != Me.CubeGrid);
			}
			else if (detachBlockType == 0)
			{
				isDetached = !((detachBlock as IMyShipMergeBlock).IsConnected);
			}
			else if (detachBlockType == 1)
			{
				isDetached = !((detachBlock as IMyMechanicalConnectionBlock).IsAttached);
			}
			else if (detachBlockType == 2)
			{
				isDetached = ((detachBlock as IMyShipConnector).Status != MyShipConnectorStatus.Connected);
			}

			if (isDetached)
			{
				subMode = 3;
				return;
			}
			else
			{
				subCounter++;

				if (subCounter >= MERGE_SEPARATE_WAIT_THRESHOLD)
				{
					Echo("Error: Missile detach failed.");
					throw new Exception("--- Initialization Failed ---");
				}

				return;
			}
		}
		else if (subMode == 3)
		{
			if (missileDetachPortType == 3 || missileDetachPortType == 4)
			{
				DetachLockedConnectors();
			}

			if (notMissile != null)
			{
				notMissileRadius = ComputeBlockGridDiagonalVector(notMissile).Length() / 2.0;
			}

			if (!InitMissileBlocks())
			{
				throw new Exception("--- Initialization Failed ---");
			}
		}

		if (missileLaunchType == 99)
		{
			mode = 99;
		}
		else
		{
			nextLidarTriggerTicks = LIDAR_REFRESH_INTERVAL;

			if (waitForHomingTrigger)
			{
				subCounter = long.MaxValue;
			}
			else
			{
				if (missileLaunchType == 6)
				{
					refFwdSet = true;
				}

				subCounter = (long)(launchSeconds * SECOND);
			}

			FireThrusters(verticalTakeoff ? launchThrusters : thrusters, true);

			mode = -1;
		}

		if (missileActivationCommands != null && missileActivationCommands.Length > 0)
		{
			ExecuteTriggerCommand(missileActivationCommands);
		}

		isLidarMode = "0,1,2,5,7".Contains(missileLaunchType.ToString());

		subMode = 0;
		delta = 0;
		clock = 0;

		init = true;
		return;
	}

	//---------- Modes And Controls ----------

	long ticks = Runtime.TimeSinceLastRun.Ticks;
	if (ticks == 160000) ticks = 166667;

	delta += ticks;
	clock += ticks;

	if (arguments != null && arguments.Length > 0)
	{
		ProcessCommunicationMessage(arguments);
	}

	if (enableMissileCommand)
	{
		ProcessMissileCommand(shipRefPanel.CustomData);
	}

	if (delta < MIN_RUN_TICKS)
	{
		return;
	}

	CalculateParameters();

	if (mode == -1)
	{
		if (waitForHomingTrigger)
		{
			if (homingReleaseLock)
			{
				subCounter = 0;
			}
		}

		if (subCounter > 0)
		{
			subCounter -= delta;
		}
		else
		{
			if (verticalTakeoff)
			{
				FireThrusters(launchThrusters, false);
				FireThrusters(thrusters, true);
			}

			SetGyroOverride(true);

			if (spinAmount > 0)
			{
				SetGyroRoll(spinAmount);
			}

			distToTarget = 1000000;

			if (missileLaunchType == 3 || missileLaunchType == 4 || missileLaunchType == 6)
			{
				if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
				{
					ExecuteTriggerCommand(missileTriggerCommands);
				}
			}

			lastTargetPosition = targetPosition = GetFlyStraightVector();

			subCounter = 0;
			subMode = 0;
			mode = missileLaunchType;
		}
	}
	else if (mode == 0)
	{
		if (subMode == 0)
		{
			if (nextLidarTriggerTicks <= clock || commsLidarTargetSet)
			{
				bool targetFound = false;

				if (commsLidarTargetSet)
				{
					commsLidarTargetSet = false;

					CheckAndSetValidLidarTarget(ref commsLidarTarget, ref refWorldMatrix, ref targetFound, false);
				}
				else if (shipRefFwd != null)
				{
					MatrixD shipRefWorldMatrix = shipRefFwd.WorldMatrix;
					Vector3D shipRefFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefWorldMatrix.Forward);
					Vector3D shipRefTargetPosition = shipRefWorldMatrix.Translation + (shipRefFwdVector * LIDAR_MAX_LOCK_DISTANCE);

					int lidarCount = fivePointInitialLockDist > 0 ? 5 : 1;
					List<IMyCameraBlock> aimLidars = GetAvailableLidars(shipRefLidars, ref shipRefTargetPosition, 0, lidarStaggerIndex++, lidarCount);
					if (aimLidars.Count >= lidarCount)
					{
						RecountLidarTicks(LIDAR_MAX_LOCK_DISTANCE, ComputeTicksFactor(shipRefLidars, ref shipRefTargetPosition, 0, lidarCount));

						Vector3D[] refVectors = null;
						if (aimLidars.Count > 1)
						{
							MatrixD matrix = MatrixD.CreateFromDir(shipRefFwdVector);
							refVectors = new Vector3D[] { shipRefTargetPosition, matrix.Up, matrix.Left };
						}

						for (int i = 0; i < aimLidars.Count; i++)
						{
							MyDetectedEntityInfo entityInfo = aimLidars[i].Raycast(i == 0 ? shipRefTargetPosition : GetSpreadPoint(refVectors, i));
							if (!entityInfo.IsEmpty())
							{
								CheckAndSetValidLidarTarget(ref entityInfo, ref shipRefWorldMatrix, ref targetFound, false);
								if (targetFound) break;
							}
						}
					}
				}

				if (targetFound)
				{
					double overshootDistance = targetRadius / 2;
					Vector3D lidarPosition = lidarTargetInfo.Position;

					IMyCameraBlock syncLidar = GetAvailableLidar(missileLidars, ref lidarPosition, overshootDistance, lidarStaggerIndex++);
					if (syncLidar != null)
					{
						RecountLidarTicks(ref lidarPosition, overshootDistance, ComputeTicksFactor(missileLidars, ref lidarPosition, overshootDistance));

						Vector3D testTargetPosition = lidarTargetInfo.Position + (Vector3D.Normalize(lidarTargetInfo.Position - syncLidar.GetPosition()) * overshootDistance);

						MyDetectedEntityInfo entityInfo = syncLidar.Raycast(testTargetPosition);
						if (!entityInfo.IsEmpty())
						{
							if (entityInfo.EntityId == lidarTargetInfo.EntityId)
							{
								TriggerLockAlert();

								if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
								{
									ExecuteTriggerCommand(missileTriggerCommands);
								}

								subCounter = 0;
								subMode = 1;
							}
						}
					}
				}
			}

			if (targetPositionSet)
			{
				targetPosition = lidarTargetInfo.Position + (lidarTargetInfo.Velocity / SECOND * (clock - lastTargetPositionClock));

				if (boolLeadTarget == true)
				{
					CalculateLeadParameters();
				}
			}
		}
		else if (subMode == 1)
		{
			PerformLidarLogic();

			if (boolLeadTarget == true)
			{
				CalculateLeadParameters();
			}
		}

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 1)
	{
		if (subMode == 0)
		{
			if (commsPositionSet)
			{
				commsPositionSet = false;

				targetPosition = commsPosition;
				targetPositionSet = true;
			}
			else
			{
				Vector3D parsedVector;
				if (shipRefPanel != null && ParseCoordinates(shipRefPanel.GetPublicTitle(), out parsedVector))
				{
					targetPosition = parsedVector;
					targetPositionSet = true;
				}
				else
				{
					lastTargetPosition = targetPosition = GetFlyStraightVector();
					targetPositionSet = false;
				}
			}

			if (targetPositionSet && (GetMissileMidPoint() - targetPosition).Length() < 1)
			{
				lastTargetPosition = targetPosition = GetFlyStraightVector();
				targetPositionSet = false;
			}

			if (targetPositionSet)
			{
				if (nextLidarTriggerTicks <= clock)
				{
					double overshootDistance = targetRadius / 2;

					IMyCameraBlock syncLidar = GetAvailableLidar(missileLidars, ref targetPosition, overshootDistance, lidarStaggerIndex++);
					if (syncLidar != null)
					{
						RecountLidarTicks(ref targetPosition, overshootDistance, ComputeTicksFactor(missileLidars, ref targetPosition, overshootDistance));

						Vector3D testTargetPosition = targetPosition + (Vector3D.Normalize(targetPosition - syncLidar.GetPosition()) * overshootDistance);

						MyDetectedEntityInfo entityInfo = syncLidar.Raycast(testTargetPosition);
						if (!entityInfo.IsEmpty())
						{
							bool targetFound = false;
							CheckAndSetValidLidarTarget(ref entityInfo, ref refWorldMatrix, ref targetFound, false);
							if (targetFound)
							{
								TriggerLockAlert();

								if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
								{
									ExecuteTriggerCommand(missileTriggerCommands);
								}

								subCounter = 0;
								subMode = 1;
							}
						}
					}
				}
			}

			if (boolLeadTarget == true)
			{
				CalculateTargetInfo();
				CalculateLeadParameters();
			}
		}
		else if (subMode == 1)
		{
			PerformLidarLogic();

			if (boolLeadTarget == true)
			{
				CalculateLeadParameters();
			}
		}

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 2)
	{
		if (subMode == 0)
		{
			if (commsFwdSet)
			{
				commsFwdSet = false;

				refFwdPosition = commsFwd.Position;
				refFwdVector = commsFwd.Direction;
			}
			else if (shipRefFwd != null)
			{
				refFwdPosition = shipRefFwd.WorldMatrix.Translation;
				refFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefFwd.WorldMatrix.Forward);
			}

			Vector3D shipToMissileVector = midPoint - refFwdPosition;
			Vector3D missileToViewLineVector = Vector3D.Reject(shipToMissileVector, refFwdVector);

			double extraDistanceExtend = Math.Min(Math.Max(5.6713 * missileToViewLineVector.Length(), speed * 2), speed * 4);
			extraDistanceExtend += (shipToMissileVector - missileToViewLineVector).Length();

			targetPosition = refFwdPosition + (refFwdVector * extraDistanceExtend);
			targetPositionSet = true;

			if (nextLidarTriggerTicks <= clock)
			{
				Vector3D shipRefTargetPosition = refFwdPosition + (refFwdVector * LIDAR_MAX_LOCK_DISTANCE);

				int lidarCount = fivePointInitialLockDist > 0 ? 5 : 1;
				List<IMyCameraBlock> syncLidars = GetAvailableLidars(missileLidars, ref shipRefTargetPosition, 0, lidarStaggerIndex++, lidarCount);
				if (syncLidars.Count >= lidarCount)
				{
					RecountLidarTicks(LIDAR_MAX_LOCK_DISTANCE, ComputeTicksFactor(missileLidars, ref shipRefTargetPosition, 0));

					Vector3D[] refVectors = null;
					if (syncLidars.Count > 1)
					{
						MatrixD matrix = MatrixD.CreateFromDir(refFwdVector);
						refVectors = new Vector3D[] { shipRefTargetPosition, matrix.Up, matrix.Left };
					}

					for (int i = 0; i < syncLidars.Count; i++)
					{
						MyDetectedEntityInfo entityInfo = syncLidars[i].Raycast(i == 0 ? shipRefTargetPosition : GetSpreadPoint(refVectors, i));
						if (!entityInfo.IsEmpty())
						{
							bool targetFound = false;
							CheckAndSetValidLidarTarget(ref entityInfo, ref refWorldMatrix, ref targetFound, false);
							if (targetFound)
							{
								TriggerLockAlert();

								if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
								{
									ExecuteTriggerCommand(missileTriggerCommands);
								}

								subCounter = 0;
								subMode = 1;

								break;
							}
						}
					}
				}
			}
		}
		else if (subMode == 1)
		{
			PerformLidarLogic();

			if (boolLeadTarget == true)
			{
				CalculateLeadParameters();
			}
		}

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 3)
	{
		if (commsFwdSet)
		{
			commsFwdSet = false;

			refFwdPosition = commsFwd.Position;
			refFwdVector = commsFwd.Direction;
		}
		else if (shipRefFwd != null)
		{
			refFwdPosition = shipRefFwd.WorldMatrix.Translation;
			refFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefFwd.WorldMatrix.Forward);
		}

		Vector3D shipToMissileVector = midPoint - refFwdPosition;
		Vector3D missileToViewLineVector = Vector3D.Reject(shipToMissileVector, refFwdVector);

		double extraDistanceExtend = Math.Min(Math.Max(5.6713 * missileToViewLineVector.Length(), speed * 2), speed * 4);
		extraDistanceExtend += (shipToMissileVector - missileToViewLineVector).Length();

		targetPosition = refFwdPosition + (refFwdVector * extraDistanceExtend);
		targetPositionSet = true;

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 4)
	{
		if (commsPositionSet)
		{
			commsPositionSet = false;

			targetPosition = commsPosition;
			targetPositionSet = true;
		}
		else if (shipRefPanel != null)
		{
			Vector3D parsedVector;
			if (ParseCoordinates(shipRefPanel.GetPublicTitle(), out parsedVector))
			{
				targetPosition = parsedVector;
				targetPositionSet = true;
			}
		}

		if (boolLeadTarget == true)
		{
			CalculateTargetInfo();
			CalculateLeadParameters();
		}

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 5)
	{
		if (subMode == 0)
		{
			if (nextLidarTriggerTicks <= clock || commsLidarTargetSet)
			{
				bool targetFound = false;

				if (commsLidarTargetSet)
				{
					commsLidarTargetSet = false;

					CheckAndSetValidLidarTarget(ref commsLidarTarget, ref refWorldMatrix, ref targetFound, true);
				}
				else if (shipRefFwd != null)
				{
					MatrixD shipRefWorldMatrix = shipRefFwd.WorldMatrix;
					Vector3D shipRefFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefWorldMatrix.Forward);
					Vector3D shipRefTargetPosition = shipRefWorldMatrix.Translation + (shipRefFwdVector * LIDAR_MAX_LOCK_DISTANCE);

					int lidarCount = fivePointInitialLockDist > 0 ? 5 : 1;
					List<IMyCameraBlock> aimLidars = GetAvailableLidars(shipRefLidars, ref shipRefTargetPosition, 0, lidarStaggerIndex++, lidarCount);
					if (aimLidars.Count >= lidarCount)
					{
						RecountLidarTicks(LIDAR_MAX_LOCK_DISTANCE, ComputeTicksFactor(shipRefLidars, ref shipRefTargetPosition, 0, lidarCount));

						Vector3D[] refVectors = null;
						if (aimLidars.Count > 1)
						{
							MatrixD matrix = MatrixD.CreateFromDir(shipRefFwdVector);
							refVectors = new Vector3D[] { shipRefTargetPosition, matrix.Up, matrix.Left };
						}

						for (int i = 0; i < aimLidars.Count; i++)
						{
							MyDetectedEntityInfo entityInfo = aimLidars[i].Raycast(i == 0 ? shipRefTargetPosition : GetSpreadPoint(refVectors, i));
							if (!entityInfo.IsEmpty())
							{
								CheckAndSetValidLidarTarget(ref entityInfo, ref shipRefWorldMatrix, ref targetFound, true);
								if (targetFound) break;
							}
						}
					}
				}

				if (targetFound)
				{
					double overshootDistance = targetRadius / 2;
					Vector3D lidarPosition = lidarTargetInfo.Position;

					IMyCameraBlock syncLidar = GetAvailableLidar(missileLidars, ref lidarPosition, overshootDistance, lidarStaggerIndex++);
					if (syncLidar != null)
					{
						RecountLidarTicks(ref lidarPosition, overshootDistance, ComputeTicksFactor(missileLidars, ref lidarPosition, overshootDistance));

						Vector3D testTargetPosition = lidarTargetInfo.Position + (Vector3D.Normalize(lidarTargetInfo.Position - syncLidar.GetPosition()) * overshootDistance);

						MyDetectedEntityInfo entityInfo = syncLidar.Raycast(testTargetPosition);
						if (!entityInfo.IsEmpty())
						{
							if (entityInfo.EntityId == lidarTargetInfo.EntityId)
							{
								TriggerLockAlert();

								if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
								{
									ExecuteTriggerCommand(missileTriggerCommands);
								}

								subCounter = 0;
								subMode = 1;
							}
						}
					}
				}
			}

			if (targetPositionSet)
			{
				targetPosition = lidarTargetInfo.Position + (lidarTargetInfo.Velocity / SECOND * (clock - lastTargetPositionClock));
				targetPosition += Vector3D.Transform(offsetTargetPosition, lidarTargetInfo.Orientation);

				if (boolLeadTarget == true)
				{
					CalculateLeadParameters();
				}
			}
		}
		else if (subMode == 1)
		{
			targetPosition = lidarTargetInfo.Position + (lidarTargetInfo.Velocity / SECOND * (clock - lastTargetPositionClock));
			targetPosition += Vector3D.Transform(offsetTargetPosition, lidarTargetInfo.Orientation);

			if (nextLidarTriggerTicks <= clock)
			{
				bool targetFound = false;
				double overshootDistance = targetRadius / 2;

				IMyCameraBlock aimLidar = GetAvailableLidar(missileLidars, ref targetPosition, overshootDistance, lidarStaggerIndex++);
				if (aimLidar != null)
				{
					RecountLidarTicks(ref targetPosition, overshootDistance, ComputeTicksFactor(missileLidars, ref targetPosition, overshootDistance));

					Vector3D testTargetPosition = targetPosition + (Vector3D.Normalize(targetPosition - aimLidar.GetPosition()) * overshootDistance);

					MyDetectedEntityInfo entityInfo = aimLidar.Raycast(testTargetPosition);
					if (!entityInfo.IsEmpty())
					{
						CheckAndUpdateLidarTarget(ref entityInfo, ref targetFound);
					}
				}

				targetPositionSet = targetFound;
			}

			if (boolLeadTarget == true)
			{
				CalculateLeadParameters();
			}
		}

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 6)
	{
		if (!refFwdSet)
		{
			if (commsFwdSet)
			{
				commsFwdSet = false;

				refFwdPosition = commsFwd.Position;
				refFwdVector = commsFwd.Direction;
				refFwdSet = true;
			}
			else if (shipRefFwd != null)
			{
				refFwdPosition = shipRefFwd.WorldMatrix.Translation;
				refFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefFwd.WorldMatrix.Forward);
				refFwdSet = true;
			}
		}

		Vector3D shipToMissileVector = midPoint - refFwdPosition;
		Vector3D missileToViewLineVector = Vector3D.Reject(shipToMissileVector, refFwdVector);

		double extraDistanceExtend = Math.Min(Math.Max(5.6713 * missileToViewLineVector.Length(), speed * 2), speed * 4);
		extraDistanceExtend += (shipToMissileVector - missileToViewLineVector).Length();

		targetPosition = refFwdPosition + (refFwdVector * extraDistanceExtend);
		targetPositionSet = true;

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 7)
	{
		if (subMode == 0)
		{
			if (nextLidarTriggerTicks <= clock || commsLidarTargetSet)
			{
				if (commsLidarTargetSet)
				{
					commsLidarTargetSet = false;

					bool targetFound = false;
					CheckAndSetValidLidarTarget(ref commsLidarTarget, ref refWorldMatrix, ref targetFound, false);
					if (targetFound)
					{
						TriggerLockAlert();

						if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
						{
							ExecuteTriggerCommand(missileTriggerCommands);
						}

						subCounter = 0;
						subMode = 1;
					}
				}
				else if (shipRefFwd != null)
				{
					MatrixD shipRefWorldMatrix = shipRefFwd.WorldMatrix;
					Vector3D shipRefFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefWorldMatrix.Forward);
					Vector3D shipRefTargetPosition = shipRefWorldMatrix.Translation + (shipRefFwdVector * LIDAR_MAX_LOCK_DISTANCE);

					int lidarCount = fivePointInitialLockDist > 0 ? 5 : 1;
					List<IMyCameraBlock> aimLidars = GetAvailableLidars(shipRefLidars, ref shipRefTargetPosition, 0, lidarStaggerIndex++, lidarCount);
					if (aimLidars.Count >= lidarCount)
					{
						RecountLidarTicks(LIDAR_MAX_LOCK_DISTANCE, ComputeTicksFactor(shipRefLidars, ref shipRefTargetPosition, 0, lidarCount));

						Vector3D[] refVectors = null;
						if (aimLidars.Count > 1)
						{
							MatrixD matrix = MatrixD.CreateFromDir(shipRefFwdVector);
							refVectors = new Vector3D[] { shipRefTargetPosition, matrix.Up, matrix.Left };
						}

						for (int i = 0; i < aimLidars.Count; i++)
						{
							MyDetectedEntityInfo entityInfo = aimLidars[i].Raycast(i == 0 ? shipRefTargetPosition : GetSpreadPoint(refVectors, i));
							if (!entityInfo.IsEmpty())
							{
								bool targetFound = false;
								CheckAndSetValidLidarTarget(ref entityInfo, ref shipRefWorldMatrix, ref targetFound, false);
								if (targetFound)
								{
									TriggerLockAlert();

									if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
									{
										ExecuteTriggerCommand(missileTriggerCommands);
									}

									subCounter = 0;
									subMode = 1;
								}
							}
						}
					}
				}
			}

			if (targetPositionSet)
			{
				targetPosition = lidarTargetInfo.Position + (lidarTargetInfo.Velocity / SECOND * (clock - lastTargetPositionClock));

				if (boolLeadTarget == true)
				{
					CalculateLeadParameters();
				}
			}
		}
		else if (subMode == 1)
		{
			targetPosition = lidarTargetInfo.Position + (lidarTargetInfo.Velocity / SECOND * (clock - lastTargetPositionClock));

			if (nextLidarTriggerTicks <= clock || commsLidarTargetSet)
			{
				bool targetFound = false;
				double overshootDistance = targetRadius / 2;

				if (commsLidarTargetSet)
				{
					commsLidarTargetSet = false;

					CheckAndUpdateLidarTarget(ref commsLidarTarget, ref targetFound);
				}
				else if (shipRefFwd != null)
				{
					IMyCameraBlock aimLidar = GetAvailableLidar(shipRefLidars, ref targetPosition, overshootDistance, lidarStaggerIndex++);
					if (aimLidar != null)
					{
						RecountLidarTicks(ref targetPosition, overshootDistance, ComputeTicksFactor(shipRefLidars, ref targetPosition, overshootDistance));

						Vector3D testTargetPosition = targetPosition + (Vector3D.Normalize(targetPosition - aimLidar.GetPosition()) * overshootDistance);

						MyDetectedEntityInfo entityInfo = aimLidar.Raycast(testTargetPosition);
						if (!entityInfo.IsEmpty())
						{
							CheckAndUpdateLidarTarget(ref entityInfo, ref targetFound);
						}
					}
				}

				targetPositionSet = targetFound;
			}

			if (boolLeadTarget == true)
			{
				CalculateLeadParameters();
			}
		}

		CalculateTargetParameters();
		AimAtTarget();

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (haveTriggerCommands)
		{
			ProcessTriggerCommands();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}
	else if (mode == 8)
	{
		if (subMode == 0)
		{
			if (homingTurret != null)
			{
				turretVectorFilter = new VectorAverageFilter(TURRET_AI_AVERAGE_SIZE);

				homingTurret.EnableIdleRotation = false;
				homingTurret.ApplyAction("OnOff_On");

				subCounter = 0;
				subMode = 1;
			}
		}
		else if (subMode == 1)
		{
			if (homingTurret.HasTarget)
			{
				lastTargetPosition = refFwdVector = CalculateTurretViewVector(homingTurret);
				turretVectorFilter.Set(ref refFwdVector);

				if (missileTriggerCommands != null && missileTriggerCommands.Length > 0)
				{
					ExecuteTriggerCommand(missileTriggerCommands);
				}

				subCounter = 0;
				subMode = 2;
			}
		}
		else if (subMode == 2)
		{
			if (homingTurret.HasTarget)
			{
				refFwdVector = CalculateTurretViewVector(homingTurret);
				turretVectorFilter.Filter(ref refFwdVector, out refFwdVector);

				targetVector = refFwdVector;

				if (boolLeadTarget == true)
				{
					targetVector += (refFwdVector - lastTargetPosition) * TURRET_AI_PN_CONSTANT;
					targetVector.Normalize();

					lastTargetPosition = refFwdVector;
				}
			}
			else
			{
				targetVector = refFwdVector;
			}

			if (boolDrift == true && speed >= 5)
			{
				targetVector = (targetVector * speed) - (driftVector / driftVectorReduction * 0.5);
				targetVector.Normalize();
			}

			targetVector = Vector3D.TransformNormal(targetVector, refViewMatrix);
			targetVector.Normalize();

			if (Double.IsNaN(targetVector.Sum))
			{
				targetVector = new Vector3D(Z_VECTOR);
			}

			AimAtTarget();

			if (haveTriggerCommands)
			{
				ProcessTriggerCommands();
			}
		}

		if (boolNaturalDampener == true)
		{
			AimAtNaturalGravity();
		}

		if (hasProximitySensors)
		{
			CheckProximitySensors();
		}
	}

	if (statusDisplay != null)
	{
		if (mode == -2)
		{
			DisplayStatus("Idle");
		}
		else if (mode == -1)
		{
			DisplayStatus("Launching");
		}
		else if (mode == 0 || mode == 1 || mode == 5 || mode == 7)
		{
			if (subMode == 0)
			{
				DisplayStatus("Initial Lock");
			}
			else if (subMode == 1)
			{
				DisplayStatus((targetPositionSet ? "Lock" : "Trace") + ": [" + Math.Round(targetPosition.GetDim(0), 2) + "," + Math.Round(targetPosition.GetDim(1), 2) + "," + Math.Round(targetPosition.GetDim(2), 2) + "]");
			}
			else
			{
				DisplayStatus("-");
			}
		}
		else if (mode == 2)
		{
			if (subMode == 0)
			{
				DisplayStatus("Initial Camera Lock");
			}
			else if (subMode == 1)
			{
				DisplayStatus((targetPositionSet ? "Lock" : "Trace") + ": [" + Math.Round(targetPosition.GetDim(0), 2) + "," + Math.Round(targetPosition.GetDim(1), 2) + "," + Math.Round(targetPosition.GetDim(2), 2) + "]");
			}
			else
			{
				DisplayStatus("-");
			}
		}
		else if (mode == 3)
		{
			DisplayStatus("Camera");
		}
		else if (mode == 4)
		{
			DisplayStatus("Cruise: [" + Math.Round(targetPosition.GetDim(0), 2) + "," + Math.Round(targetPosition.GetDim(1), 2) + "," + Math.Round(targetPosition.GetDim(2), 2) + "]");
		}
		else if (mode == 6)
		{
			DisplayStatus("Fixed Glide");
		}
		else if (mode == 8)
		{
			if (subMode == 2)
			{
				DisplayStatus("Turret Locked");
			}
			else if (subMode == 0 || subMode == 1)
			{
				DisplayStatus("Initial Lock");
			}
			else
			{
				DisplayStatus("-");
			}
		}
		else
		{
			DisplayStatus("-");
		}
	}

	if (outputMissileStatus)
	{
		string statusCode;
		switch (mode)
		{
		case -2:
			statusCode = "-";
			break;
		case -1:
			statusCode = (waitForHomingTrigger ? "W" : (subCounter > 0 ? "F" : "K"));
			break;
		case 0:
		case 1:
		case 5:
		case 7:
			statusCode = (subMode == 0 ? "K" : (targetPositionSet ? "L" : "T"));
			break;
		case 2:
			statusCode = (subMode == 0 ? "C" : (targetPositionSet ? "L" : "T"));
			break;
		case 3:
			statusCode = "C";
			break;
		case 4:
			statusCode = "D";
			break;
		case 6:
			statusCode = "G";
			break;
		case 8:
			statusCode = (subMode == 2 ? "U" : "K");
			break;
		default:
			statusCode = "-";
			break;
		}
		Echo("ST:" + mode + ":" + subMode + ":" + (waitForHomingTrigger ? 0 : subCounter) + ":" + clock + ":" + statusCode + ":" +
		Math.Round(targetPosition.GetDim(0), 5) + ":" + Math.Round(targetPosition.GetDim(1), 5) + ":" + Math.Round(targetPosition.GetDim(2), 5) + ":" +
		Math.Round(targetRadius, 5) + ":");
	}

	delta = 0;
}

//------------------------------ Miscellaneous Methods ------------------------------

void DisplayStatus(string statusMsg)
{
	if (statusDisplay != null)
	{
		statusDisplay.CustomName = strStatusDisplayPrefix + " Mode: " + mode + ", " + statusMsg;
	}
}

void TriggerLockAlert()
{
	if (alertBlock != null)
	{
		if (alertBlock.HasAction(strLockTriggerAction))
		{
			alertBlock.ApplyAction(strLockTriggerAction);
		}
	}
}

Vector3D GetMissileMidPoint()
{
	return (Me.CubeGrid.GridIntegerToWorld(Me.CubeGrid.Min) + Me.CubeGrid.GridIntegerToWorld(Me.CubeGrid.Max)) / 2;
}

Vector3D GetFlyStraightVector()
{
	return (driftVector * 1000000) + midPoint;
}

//------------------------------ Missile And Target Information Methods ------------------------------

void CalculateParameters()
{
	//---------- Calculate Missile Related Variables ----------

	refWorldMatrix = refFwdBlock.WorldMatrix;
	refViewMatrix = MatrixD.Transpose(refWorldMatrix);
	if (refFwdReverse)
	{
		refViewMatrix.M11 = -refViewMatrix.M11;
		refViewMatrix.M21 = -refViewMatrix.M21;
		refViewMatrix.M31 = -refViewMatrix.M31;
		refViewMatrix.M13 = -refViewMatrix.M13;
		refViewMatrix.M23 = -refViewMatrix.M23;
		refViewMatrix.M33 = -refViewMatrix.M33;
	}

	if (remoteControl != null)
	{
		midPoint = remoteControl.GetPosition();
		driftVector = remoteControl.GetShipVelocities().LinearVelocity;
		speed = driftVector.Length();

		naturalGravity = remoteControl.GetNaturalGravity();
		naturalGravityLength = naturalGravity.Length();
		naturalGravity = (naturalGravityLength > 0 ? naturalGravity / naturalGravityLength : POINT_ZERO);
	}
	else
	{
		midPoint = GetMissileMidPoint();
		driftVector = midPoint - lastMidPoint;
		speed = driftVector.Length() * (SECOND / delta);

		naturalGravity = driftVector;
		naturalGravityLength = naturalGravity.Length();
		naturalGravity = (naturalGravityLength > 0 ? naturalGravity / naturalGravityLength : POINT_ZERO);

		lastMidPoint = midPoint;
	}

	rpm = Math.Acos(lastNormal.Dot(refWorldMatrix.Up)) * RPM_FACTOR;
	lastNormal = refWorldMatrix.Up;
}

void CalculateTargetInfo()
{
	if (targetPositionSet)
	{
		targetDirection = targetPosition - lastTargetPosition;
		targetSpeed = targetDirection.Length();

		if (targetSpeed > 0)
		{
			targetDirection = targetDirection / targetSpeed;
			targetSpeed = targetSpeed * SECOND / (clock - lastTargetPositionClock);
		}

		lastTargetPosition = targetPosition;
		lastTargetPositionClock = clock;

		targetPositionSet = false;
	}
	else
	{
		targetPosition = lastTargetPosition + ((targetDirection * targetSpeed) / SECOND * (clock - lastTargetPositionClock));
	}
}

void CalculateLeadParameters()
{
	if (targetSpeed > 0)
	{
		Vector3D aimPosition = ComputeIntersectionPoint(targetDirection, targetPosition, targetSpeed, midPoint, speed);
		if (!Double.IsNaN(aimPosition.Sum))
		{
			targetPosition = aimPosition;
		}
	}
}

void CalculateTargetParameters()
{
	//---------- Calculate Target Parameters ----------

	targetVector = targetPosition - midPoint;
	distToTarget = targetVector.Length();
	targetVector = targetVector / distToTarget;

	if (boolDrift == true && speed >= 5)
	{
		targetVector = (targetVector * speed) - (driftVector / driftVectorReduction);
		targetVector.Normalize();
	}

	targetVector = Vector3D.TransformNormal(targetVector, refViewMatrix);
	targetVector.Normalize();

	if (Double.IsNaN(targetVector.Sum))
	{
		targetVector = new Vector3D(Z_VECTOR);
	}
}

Vector3D CalculateTurretViewVector(IMyLargeTurretBase turret)
{
	Vector3D direction;
	Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out direction);

	return Vector3D.TransformNormal(direction, turret.WorldMatrix);
}

Vector3D GetSpreadPoint(Vector3D[] refVectors, int index)
{
	switch (index)
	{
	case 0: return refVectors[0];
	case 1: return refVectors[0] + (refVectors[1] * fivePointInitialLockDist);
	case 2: return refVectors[0] + (-refVectors[1] * fivePointInitialLockDist);
	case 3: return refVectors[0] + (refVectors[2] * fivePointInitialLockDist);
	case 4: return refVectors[0] + (-refVectors[2] * fivePointInitialLockDist);
	default: return refVectors[0];
	}
}

//------------------------------ Missile Lock-On And Leading Methods ------------------------------

void PerformLidarLogic()
{
	targetPosition = lidarTargetInfo.Position + (lidarTargetInfo.Velocity / SECOND * (clock - lastTargetPositionClock));

	if (nextLidarTriggerTicks <= clock)
	{
		bool targetFound = false;
		double overshootDistance = targetRadius / 2;

		IMyCameraBlock aimLidar = GetAvailableLidar(missileLidars, ref targetPosition, overshootDistance, lidarStaggerIndex++);
		if (aimLidar != null)
		{
			RecountLidarTicks(ref targetPosition, overshootDistance, ComputeTicksFactor(missileLidars, ref targetPosition, overshootDistance));

			Vector3D testTargetPosition = targetPosition + (Vector3D.Normalize(targetPosition - aimLidar.GetPosition()) * overshootDistance);

			MyDetectedEntityInfo entityInfo = aimLidar.Raycast(testTargetPosition);
			if (!entityInfo.IsEmpty())
			{
				if (entityInfo.EntityId == lidarTargetInfo.EntityId)
				{
					distToTarget = Vector3D.Distance(entityInfo.Position, refWorldMatrix.Translation);
					targetSpeed = entityInfo.Velocity.Length();
					targetDirection = (targetSpeed > 0 ? new Vector3D(entityInfo.Velocity) / targetSpeed : new Vector3D());
					targetRadius = Vector3D.Distance(entityInfo.BoundingBox.Min, entityInfo.BoundingBox.Max);

					lidarTargetInfo = entityInfo;
					lastTargetPositionClock = clock;

					targetPosition = entityInfo.Position;
					targetFound = true;
				}
			}
		}

		targetPositionSet = targetFound;
	}
}

void CheckAndSetValidLidarTarget(ref MyDetectedEntityInfo entityInfo, ref MatrixD shipRefWorldMatrix, ref bool targetFound, bool setOffset = false)
{
	if (IsValidLidarTarget(ref entityInfo, ref shipRefWorldMatrix))
	{
		distToTarget = Vector3D.Distance(entityInfo.Position, refWorldMatrix.Translation);
		targetSpeed = entityInfo.Velocity.Length();
		targetDirection = (targetSpeed > 0 ? new Vector3D(entityInfo.Velocity) / targetSpeed : new Vector3D());
		targetRadius = Vector3D.Distance(entityInfo.BoundingBox.Min, entityInfo.BoundingBox.Max);

		if (setOffset)
		{
			if (entityInfo.HitPosition != null)
			{
				offsetTargetPosition = Vector3D.Transform(entityInfo.HitPosition.Value - entityInfo.Position, MatrixD.Invert(entityInfo.Orientation));
			}
			else
			{
				offsetTargetPosition = POINT_ZERO;
			}
		}

		lidarTargetInfo = entityInfo;
		lastTargetPositionClock = clock;

		targetPositionSet = targetFound = true;
	}
}

void CheckAndUpdateLidarTarget(ref MyDetectedEntityInfo entityInfo, ref bool targetFound)
{
	if (entityInfo.EntityId == lidarTargetInfo.EntityId)
	{
		distToTarget = Vector3D.Distance(entityInfo.Position, refWorldMatrix.Translation);
		targetSpeed = entityInfo.Velocity.Length();
		targetDirection = (targetSpeed > 0 ? new Vector3D(entityInfo.Velocity) / targetSpeed : new Vector3D());
		targetRadius = Vector3D.Distance(entityInfo.BoundingBox.Min, entityInfo.BoundingBox.Max);

		lidarTargetInfo = entityInfo;
		lastTargetPositionClock = clock;

		targetPosition = entityInfo.Position;
		targetFound = true;
	}
}

Vector3D ComputeIntersectionPoint(Vector3D targetDirection, Vector3D targetLocation, double targetSpeed, Vector3D currentLocation, double currentSpeed)
{
	//---------- Calculate Impact Point ----------


	double a = (targetSpeed * targetSpeed) - (currentSpeed * currentSpeed);
	double b = (2 * targetDirection.Dot(targetLocation - currentLocation) * targetSpeed);
	double c = (targetLocation - currentLocation).LengthSquared();

	double t;

	if (a == 0)
	{
		t = -c / a;
	}
	else
	{

		double u = (b * b) - (4 * a * c);
		if (u < 0)
		{

			return new Vector3D(Double.NaN, Double.NaN, Double.NaN);
		}
		u = Math.Sqrt(u);

		double t1 = (-b + u) / (2 * a);
		double t2 = (-b - u) / (2 * a);

		t = (t1 > 0 ? (t2 > 0 ? (t1 < t2 ? t1 : t2) : t1) : t2);
	}

	if (t < 0)
	{
		return new Vector3D(Double.NaN, Double.NaN, Double.NaN);
	}
	else
	{
		return targetLocation + (targetDirection * targetSpeed * t);
	}
}

IMyCameraBlock GetAvailableLidar(List<IMyCameraBlock> lidars, ref Vector3D aimPoint, double overshootDistance, int indexOffset)
{
	List<IMyCameraBlock> result = GetAvailableLidars(lidars, ref aimPoint, overshootDistance, indexOffset, 1);
	return (result.Count > 0 ? result[0] : null);
}

List<IMyCameraBlock> GetAvailableLidars(List<IMyCameraBlock> lidars, ref Vector3D aimPoint, double overshootDistance, int indexOffset, int lidarCount)
{
	List<IMyCameraBlock> result = new List<IMyCameraBlock>(lidarCount);

	for (int i = 0; i < lidars.Count; i++)
	{
		IMyCameraBlock lidar = lidars[(i + indexOffset) % lidars.Count];

		MatrixD lidarWorldMatrix = lidar.WorldMatrix;
		Vector3D aimVector = aimPoint - lidarWorldMatrix.Translation;
		double distance = aimVector.Length();

		if (lidar.CanScan(distance + overshootDistance))
		{
			Vector3D scaleLeft = sideScale * lidarWorldMatrix.Left;
			Vector3D scaleUp = sideScale * lidarWorldMatrix.Up;

			if (sideScale >= 0)
			{
				if (aimVector.Dot(lidarWorldMatrix.Forward + scaleLeft) >= 0 &&
						aimVector.Dot(lidarWorldMatrix.Forward - scaleLeft) >= 0 &&
						aimVector.Dot(lidarWorldMatrix.Forward + scaleUp) >= 0 &&
						aimVector.Dot(lidarWorldMatrix.Forward - scaleUp) >= 0)
				{
					result.Add(lidar);

					if (result.Count >= lidarCount) break;
				}
			}
			else
			{
				if (aimVector.Dot(lidarWorldMatrix.Forward + scaleLeft) >= 0 ||
						aimVector.Dot(lidarWorldMatrix.Forward - scaleLeft) >= 0 ||
						aimVector.Dot(lidarWorldMatrix.Forward + scaleUp) >= 0 ||
						aimVector.Dot(lidarWorldMatrix.Forward - scaleUp) >= 0)
				{
					result.Add(lidar);

					if (result.Count >= lidarCount) break;
				}
			}
		}
	}

	return result;
}

double ComputeTicksFactor(List<IMyCameraBlock> lidars, ref Vector3D aimPoint, double overshootDistance, int lidarCount = 1)
{
	if (nextLidarRecountTicks <= clock)
	{
		lidarCount = (GetAvailableLidars(lidars, ref aimPoint, overshootDistance, lidarStaggerIndex, lidars.Count).Count - lidarCount) / lidarCount;
		ticksFactor = ticksRatio / Math.Max((int)Math.Floor(lidarCount * LIDAR_REFRESH_CALC_FACTOR), 1);

		nextLidarRecountTicks = clock + MIN_RECOUNT_TICKS;
	}
	return ticksFactor;
}

void RecountLidarTicks(ref Vector3D position, double addDistance, double factor)
{
	RecountLidarTicks(Vector3D.Distance(position, refWorldMatrix.Translation) + addDistance, factor);
}
void RecountLidarTicks(double distance, double factor)
{
	if (LIDAR_REFRESH_INTERVAL == 0)
	{
		nextLidarTriggerTicks = clock + (long)Math.Ceiling(distance * factor);
	}
	else
	{
		nextLidarTriggerTicks = clock + LIDAR_REFRESH_INTERVAL;
	}
}

bool IsValidLidarTarget(ref MyDetectedEntityInfo entityInfo, ref MatrixD referenceWorldMatrix)
{
	if (entityInfo.Type != MyDetectedEntityType.Asteroid && entityInfo.Type != MyDetectedEntityType.Planet)
	{
		if (Vector3D.Distance(entityInfo.Position, referenceWorldMatrix.Translation) > LIDAR_MIN_LOCK_DISTANCE)
		{
			if (!excludeFriendly || IsNotFriendly(entityInfo.Relationship))
			{
				if (notMissile == null || (entityInfo.Position - ComputeBlockGridMidPoint(notMissile)).Length() > notMissileRadius)
				{
					if ((entityInfo.Position - referenceWorldMatrix.Translation).Length() >= LIDAR_MIN_LOCK_DISTANCE && (GetMissileMidPoint() - entityInfo.Position).Length() >= 1)
					{
						return true;
					}
				}
			}
		}
	}
	return false;
}

bool IsValidProximityTarget(ref MyDetectedEntityInfo detected)
{
	bool matchAll = (!isLidarMode && lidarTargetInfo.EntityId <= 0);

	if (detected.EntityId > 0 && (matchAll || lidarTargetInfo.EntityId == detected.EntityId))
	{
		if (!excludeFriendly || IsNotFriendly(detected.Relationship))
		{
			return true;
		}
	}
	return false;
}

bool IsNotFriendly(VRage.Game.MyRelationsBetweenPlayerAndBlock relationship)
{
	return (relationship != VRage.Game.MyRelationsBetweenPlayerAndBlock.FactionShare && relationship != VRage.Game.MyRelationsBetweenPlayerAndBlock.Owner);
}

//------------------------------ Missile Aiming Methods ------------------------------

int GetMultiplierSign(double value)
{
	return (value < 0 ? -1 : 1);
}

void AimAtTarget()
{
	//---------- Activate Gyroscopes To Turn Towards Target ----------

	Vector3D yawVector = new Vector3D(targetVector.GetDim(0), 0, targetVector.GetDim(2));
	Vector3D pitchVector = new Vector3D(0, targetVector.GetDim(1), targetVector.GetDim(2));
	yawVector.Normalize();
	pitchVector.Normalize();

	targetYawAngle = Math.Acos(yawVector.Dot(Z_VECTOR)) * GetMultiplierSign(targetVector.GetDim(0));
	targetPitchAngle = Math.Acos(pitchVector.Dot(Z_VECTOR)) * GetMultiplierSign(targetVector.GetDim(1));

	double targetYawAngleLN = Math.Round(targetYawAngle, 2);
	double targetPitchAngleLN = Math.Round(targetPitchAngle, 2);

	//---------- PID Controller Adjustment ----------

	double del = SECOND / delta;

	lastYawIntegral = lastYawIntegral + (targetYawAngle / del);
	lastYawIntegral = (INTEGRAL_WINDUP_LIMIT > 0 ? Math.Max(Math.Min(lastYawIntegral, INTEGRAL_WINDUP_LIMIT), -INTEGRAL_WINDUP_LIMIT) : lastYawIntegral);
	double yawDerivative = (targetYawAngleLN - lastYawError) * del;
	lastYawError = targetYawAngleLN;
	targetYawAngle = (AIM_P * targetYawAngle) + (AIM_I * lastYawIntegral) + (AIM_D * yawDerivative);

	lastPitchIntegral = lastPitchIntegral + (targetPitchAngle / del);
	lastPitchIntegral = (INTEGRAL_WINDUP_LIMIT > 0 ? Math.Max(Math.Min(lastPitchIntegral, INTEGRAL_WINDUP_LIMIT), -INTEGRAL_WINDUP_LIMIT) : lastPitchIntegral);
	double pitchDerivative = (targetPitchAngleLN - lastPitchError) * del;
	lastPitchError = targetPitchAngleLN;
	targetPitchAngle = (AIM_P * targetPitchAngle) + (AIM_I * lastPitchIntegral) + (AIM_D * pitchDerivative);

	if (Math.Abs(targetYawAngle) + Math.Abs(targetPitchAngle) > AIM_LIMIT)
	{
		double adjust = AIM_LIMIT / (Math.Abs(targetYawAngle) + Math.Abs(targetPitchAngle));
		targetYawAngle *= adjust;
		targetPitchAngle *= adjust;
	}

	//---------- Set Gyroscope Parameters ----------

	SetGyroYaw(targetYawAngle);
	SetGyroPitch(targetPitchAngle);
}

void AimAtNaturalGravity()
{
	//---------- Activate Gyroscopes To Aim Dampener At Natural Gravity ----------

	if (refDwdBlock == null || naturalGravityLength < 0.01)
	{
		return;
	}

	MatrixD dampenerLookAtMatrix = MatrixD.CreateLookAt(POINT_ZERO, refDwdBlock.WorldMatrix.Forward, (refFwdReverse ? refWorldMatrix.Backward : refWorldMatrix.Forward));

	Vector3D gravityVector = Vector3D.TransformNormal(naturalGravity, dampenerLookAtMatrix);
	gravityVector.SetDim(1, 0);
	gravityVector.Normalize();

	if (Double.IsNaN(gravityVector.Sum))
	{
		gravityVector = new Vector3D(Z_VECTOR);
	}

	targetRollAngle = Math.Acos(gravityVector.Dot(Z_VECTOR)) * GetMultiplierSign(gravityVector.GetDim(0));

	double targetRollAngleLN = Math.Round(targetRollAngle, 2);

	//---------- PID Controller Adjustment ----------

	double del = SECOND / delta;

	lastRollIntegral = lastRollIntegral + (targetRollAngle / del);
	lastRollIntegral = (INTEGRAL_WINDUP_LIMIT > 0 ? Math.Max(Math.Min(lastRollIntegral, INTEGRAL_WINDUP_LIMIT), -INTEGRAL_WINDUP_LIMIT) : lastRollIntegral);
	double rollDerivative = (targetRollAngleLN - lastRollError) * del;
	lastRollError = targetRollAngleLN;
	targetRollAngle = (AIM_P * targetRollAngle) + (AIM_I * lastRollIntegral) + (AIM_D * rollDerivative);

	//---------- Set Gyroscope Parameters ----------

	SetGyroRoll(targetRollAngle);
}

//------------------------------ Missile Separation Methods ------------------------------

bool DetachFromGrid(bool testOnly = false)
{
	List<IMyTerminalBlock> blocks;

	switch (missileDetachPortType)
	{
	case 0:
	case 3:
		blocks = (strDetachPortTag != null && strDetachPortTag.Length > 0 ? GetBlocksWithName<IMyShipMergeBlock>(strDetachPortTag) : GetBlocksOfType<IMyShipMergeBlock>());
		detachBlock = GetClosestBlockFromReference(blocks, Me);

		if (!testOnly)
		{
			if (detachBlock == null)
			{
				Echo("Error: Missing Merge Block " + (strDetachPortTag != null && strDetachPortTag.Length > 0 ? "with tag " + strDetachPortTag + " to detach" : "to detach."));
				return false;
			}
			detachBlockType = 0;

			detachBlock.ApplyAction("OnOff_Off");
		}
		return true;
	case 1:
	case 4:
		blocks = (strDetachPortTag != null && strDetachPortTag.Length > 0 ? GetBlocksWithName<IMyMechanicalConnectionBlock>(strDetachPortTag) : GetBlocksOfType<IMyMechanicalConnectionBlock>());
		detachBlock = GetClosestBlockFromReference(blocks, Me);

		if (!testOnly)
		{
			if (detachBlock == null)
			{
				Echo("Error: Missing Rotor " + (strDetachPortTag != null && strDetachPortTag.Length > 0 ? "with tag " + strDetachPortTag + " to detach" : "to detach."));
				return false;
			}
			detachBlockType = 1;

			detachBlock.ApplyAction("Detach");
		}
		return true;
	case 2:
		blocks = (strDetachPortTag != null && strDetachPortTag.Length > 0 ? GetBlocksWithName<IMyShipConnector>(strDetachPortTag) : GetBlocksOfType<IMyShipConnector>());
		detachBlock = GetClosestBlockFromReference(blocks, Me, true);

		if (!testOnly)
		{
			if (detachBlock == null)
			{
				Echo("Error: Missing Connector " + (strDetachPortTag != null && strDetachPortTag.Length > 0 ? "with tag " + strDetachPortTag + " to detach" : "to detach."));
				return false;
			}
			detachBlockType = 2;

			detachBlock.ApplyAction("Unlock");
		}
		return true;
	case 99:
		return true;
	default:
		if (!testOnly)
		{
			Echo("Error: Unknown missileDetachPortType - " + missileDetachPortType + ".");
		}
		return false;
	}
}

IMyTerminalBlock GetClosestBlockFromReference(List<IMyTerminalBlock> checkBlocks, IMyTerminalBlock referenceBlock, bool sameGridCheck = false)
{
	IMyTerminalBlock checkBlock = null;
	double prevCheckDistance = Double.MaxValue;

	for (int i = 0; i < checkBlocks.Count; i++)
	{
		if (!sameGridCheck || checkBlocks[i].CubeGrid == referenceBlock.CubeGrid)
		{
			double currCheckDistance = (checkBlocks[i].GetPosition() - referenceBlock.GetPosition()).Length();
			if (currCheckDistance < prevCheckDistance)
			{
				prevCheckDistance = currCheckDistance;
				checkBlock = checkBlocks[i];
			}
		}
	}

	return checkBlock;
}

IMyShipMergeBlock GetConnectedMergeBlock(IMyCubeGrid grid, IMyTerminalBlock mergeBlock)
{
	IMySlimBlock slimBlock = grid.GetCubeBlock(mergeBlock.Position - new Vector3I(Base6Directions.GetVector(mergeBlock.Orientation.Left)));
	return (slimBlock == null ? null : slimBlock.FatBlock as IMyShipMergeBlock);
}

void DetachLockedConnectors()
{
	List<IMyTerminalBlock> blocks = GetBlocksOfType<IMyShipConnector>();
	for (int i = 0; i < blocks.Count; i++)
	{
		if (blocks[i].CubeGrid == Me.CubeGrid)
		{
			IMyShipConnector otherConnector = ((IMyShipConnector)blocks[i]).OtherConnector;
			if (otherConnector == null || blocks[i].CubeGrid != otherConnector.CubeGrid)
			{
				blocks[i].ApplyAction("Unlock");
			}
		}
	}
}

//------------------------------ String Parsing Methods ------------------------------

bool ParseMatrix(string[] tokens, out MatrixD parsedMatrix, int start = 0, bool isOrientation = false)
{
	if (tokens.Length < start + (isOrientation ? 9 : 16))
	{
		parsedMatrix = new MatrixD();
		return false;
	}

	double v;
	double[] r = new double[isOrientation ? 9 : 16];

	for (int i = start; i < start + r.Length; i++)
	{
		if (Double.TryParse(tokens[i], out v))
		{
			r[i] = v;
		}
	}

	if (isOrientation)
	{
		parsedMatrix = new MatrixD(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8]);
	}
	else
	{
		parsedMatrix = new MatrixD(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10], r[11], r[12], r[13], r[14], r[15]);
	}

	return true;
}

bool ParseVector(string[] tokens, out Vector3D parsedVector, int start = 0)
{
	parsedVector = new Vector3D();

	if (tokens.Length < start + 3)
	{
		return false;
	}

	double result;

	if (Double.TryParse(tokens[start], out result))
	{
		parsedVector.SetDim(0, result);
	}
	else
	{
		return false;
	}

	if (Double.TryParse(tokens[start + 1], out result))
	{
		parsedVector.SetDim(1, result);
	}
	else
	{
		return false;
	}

	if (Double.TryParse(tokens[start + 2], out result))
	{
		parsedVector.SetDim(2, result);
	}
	else
	{
		return false;
	}

	return true;
}

bool ParseCoordinates(string coordinates, out Vector3D parsedVector)
{
	parsedVector = new Vector3D();
	coordinates = coordinates.Trim();

	double result;
	string[] tokens = coordinates.Split(':');

	if (coordinates.StartsWith("GPS") && tokens.Length >= 5)
	{
		if (Double.TryParse(tokens[2], out result))
		{
			parsedVector.SetDim(0, result);
		}
		else
		{
			return false;
		}

		if (Double.TryParse(tokens[3], out result))
		{
			parsedVector.SetDim(1, result);
		}
		else
		{
			return false;
		}

		if (Double.TryParse(tokens[4], out result))
		{
			parsedVector.SetDim(2, result);
		}
		else
		{
			return false;
		}

		return true;
	}
	else if (coordinates.StartsWith("[T:") && tokens.Length >= 4)
	{
		if (Double.TryParse(tokens[1], out result))
		{
			parsedVector.SetDim(0, result);
		}
		else
		{
			return false;
		}

		if (Double.TryParse(tokens[2], out result))
		{
			parsedVector.SetDim(1, result);
		}
		else
		{
			return false;
		}

		if (Double.TryParse(tokens[3].Substring(0, tokens[3].Length - 1), out result))
		{
			parsedVector.SetDim(2, result);
		}
		else
		{
			return false;
		}

		return true;
	}
	else
	{
		return false;
	}
}

//------------------------------ Command Processing Methods ------------------------------

void ProcessCustomConfiguration()
{
	CustomConfiguration cfg = new CustomConfiguration(Me);
	cfg.Load();

	cfg.Get("missileLaunchType", ref missileLaunchType);
	cfg.Get("missileDetachPortType", ref missileDetachPortType);
	cfg.Get("spinAmount", ref spinAmount);
	cfg.Get("verticalTakeoff", ref verticalTakeoff);
	cfg.Get("waitForHomingTrigger", ref waitForHomingTrigger);
	cfg.Get("enableMissileCommand", ref enableMissileCommand);
	cfg.Get("missileBlockSameGridOnly", ref missileBlockSameGridOnly);
	cfg.Get("fivePointInitialLockDist", ref fivePointInitialLockDist);
	cfg.Get("missileId", ref missileId);
	cfg.Get("missileGroup", ref missileGroup);
	cfg.Get("allowedSenderId", ref allowedSenderId);
	cfg.Get("strShipRefLidar", ref strShipRefLidar);
	cfg.Get("strShipRefForward", ref strShipRefFwd);
	cfg.Get("strShipRefTargetPanel", ref strShipRefPanel);
	cfg.Get("strShipRefNotMissileTag", ref strShipRefNotMissileTag);
	cfg.Get("missileActivationCommands", ref missileActivationCommands);
	cfg.Get("missileTriggerCommands", ref missileTriggerCommands);
	cfg.Get("proximityTriggerCommands", ref proximityTriggerCommands);
	cfg.Get("failunsafeTriggerCommands", ref failunsafeTriggerCommands);
	cfg.Get("strGyroscopesTag", ref strGyroscopesTag);
	cfg.Get("strThrustersTag", ref strThrustersTag);
	cfg.Get("strDetachPortTag", ref strDetachPortTag);
	cfg.Get("strDirectionRefBlockTag", ref strDirectionRefBlockTag);
	cfg.Get("strProximitySensorTag", ref strProximitySensorTag);
	cfg.Get("strLockTriggerBlockTag", ref strLockTriggerBlockTag);
	cfg.Get("strLockTriggerAction", ref strLockTriggerAction);
	cfg.Get("strStatusDisplayPrefix", ref strStatusDisplayPrefix);
	cfg.Get("driftVectorReduction", ref driftVectorReduction);
	cfg.Get("launchSeconds", ref launchSeconds);
	cfg.Get("boolDrift", ref boolDrift);
	cfg.Get("boolLeadTarget", ref boolLeadTarget);
	cfg.Get("boolNaturalDampener", ref boolNaturalDampener);
	cfg.Get("LIDAR_MIN_LOCK_DISTANCE", ref LIDAR_MIN_LOCK_DISTANCE);
	cfg.Get("LIDAR_MAX_LOCK_DISTANCE", ref LIDAR_MAX_LOCK_DISTANCE);
	cfg.Get("LIDAR_REFRESH_INTERVAL", ref LIDAR_REFRESH_INTERVAL);
	cfg.Get("LIDAR_REFRESH_CALC_FACTOR", ref LIDAR_REFRESH_CALC_FACTOR);
	cfg.Get("excludeFriendly", ref excludeFriendly);
	cfg.Get("DEF_SMALL_GRID_P", ref DEF_SMALL_GRID_P);
	cfg.Get("DEF_SMALL_GRID_I", ref DEF_SMALL_GRID_I);
	cfg.Get("DEF_SMALL_GRID_D", ref DEF_SMALL_GRID_D);
	cfg.Get("DEF_BIG_GRID_P", ref DEF_BIG_GRID_P);
	cfg.Get("DEF_BIG_GRID_I", ref DEF_BIG_GRID_I);
	cfg.Get("DEF_BIG_GRID_D", ref DEF_BIG_GRID_D);
	cfg.Get("useDefaultPIDValues", ref useDefaultPIDValues);
	cfg.Get("AIM_P", ref AIM_P);
	cfg.Get("AIM_I", ref AIM_I);
	cfg.Get("AIM_D", ref AIM_D);
	cfg.Get("AIM_LIMIT", ref AIM_LIMIT);
	cfg.Get("INTEGRAL_WINDUP_LIMIT", ref INTEGRAL_WINDUP_LIMIT);
	cfg.Get("MERGE_SEPARATE_WAIT_THRESHOLD", ref MERGE_SEPARATE_WAIT_THRESHOLD);
	cfg.Get("TURRET_AI_PN_CONSTANT", ref TURRET_AI_PN_CONSTANT);
	cfg.Get("TURRET_AI_AVERAGE_SIZE", ref TURRET_AI_AVERAGE_SIZE);
	cfg.Get("outputMissileStatus", ref outputMissileStatus);
}

void ProcessConfigurationCommand(string commandLine)
{
	string[] keyValues = commandLine.Split(',');

	for (int i = 0; i < keyValues.Length; i++)
	{
		string[] tokens = keyValues[i].Trim().Split(':');
		if (tokens.Length > 0)
		{
			ProcessSingleConfigCommand(tokens);
		}
	}
}

void ProcessSingleConfigCommand(string[] tokens)
{
	string cmdToken = tokens[0].Trim().ToUpper();
	if (cmdToken.Equals("MODE") && tokens.Length >= 2)
	{
		int modeValue;
		if (Int32.TryParse(tokens[1], out modeValue))
		{
			missileLaunchType = modeValue;
		}
	}
	else if (cmdToken.Equals("R_LDR") && tokens.Length >= 2)
	{
		strShipRefLidar = tokens[1];
	}
	else if (cmdToken.Equals("R_TAR") && tokens.Length >= 2)
	{
		strShipRefPanel = tokens[1];
	}
	else if (cmdToken.Equals("R_FWD") && tokens.Length >= 2)
	{
		strShipRefFwd = tokens[1];
	}
	else if (cmdToken.Equals("V_DVR") && tokens.Length >= 2)
	{
		double dvrValue;
		if (Double.TryParse(tokens[1], out dvrValue))
		{
			driftVectorReduction = dvrValue;
		}
	}
	else if (cmdToken.Equals("V_LS") && tokens.Length >= 2)
	{
		double lsValue;
		if (Double.TryParse(tokens[1], out lsValue))
		{
			launchSeconds = lsValue;
		}
	}
	else if (cmdToken.Equals("V_DRIFT") && tokens.Length >= 2)
	{
		bool driftValue;
		if (bool.TryParse(tokens[1], out driftValue))
		{
			boolDrift = driftValue;
		}
	}
	else if (cmdToken.Equals("V_LEAD") && tokens.Length >= 2)
	{
		bool leadValue;
		if (bool.TryParse(tokens[1], out leadValue))
		{
			boolLeadTarget = leadValue;
		}
	}
	else if (cmdToken.Equals("V_DAMP") && tokens.Length >= 2)
	{
		bool dampenerValue;
		if (bool.TryParse(tokens[1], out dampenerValue))
		{
			boolNaturalDampener = dampenerValue;
		}
	}
	else if (cmdToken.Equals("P_VT") && tokens.Length >= 2)
	{
		bool vtValue;
		if (bool.TryParse(tokens[1], out vtValue))
		{
			verticalTakeoff = vtValue;
		}
	}
	else if (cmdToken.Equals("P_WFT") && tokens.Length >= 2)
	{
		bool wftValue;
		if (bool.TryParse(tokens[1], out wftValue))
		{
			waitForHomingTrigger = wftValue;
		}
	}
	else if (cmdToken.Equals("P_EMC") && tokens.Length >= 2)
	{
		bool emcValue;
		if (bool.TryParse(tokens[1], out emcValue))
		{
			enableMissileCommand = emcValue;
		}
	}
	else if (cmdToken.Equals("SPIN") && tokens.Length >= 2)
	{
		double spinValue;
		if (Double.TryParse(tokens[1], out spinValue))
		{
			spinAmount = (int)spinValue;
		}
	}
	else if (cmdToken.Equals("CHECK") || cmdToken.Equals("CHECKMISSILE"))
	{
		subMode = 0;

		CheckMissile();
	}
	else if (cmdToken.Equals("CHECKSHIP"))
	{
		subMode = 0;

		CheckLaunchingShip();
	}
}

void ProcessTriggerCommands()
{
	if (rpmTriggerList != null && rpmTriggerList.Count > 0)
	{
		int i = 0;
		while (i < rpmTriggerList.Count)
		{
			if (rpmTriggerList[i].Key <= rpm)
			{
				ProcessSingleMissileCommand(rpmTriggerList[i].Value);
				rpmTriggerList.RemoveAt(i);
			}
			else
			{
				i++;
			}
		}
	}

	if (distTriggerList != null && distTriggerList.Count > 0)
	{
		int i = 0;
		while (i < distTriggerList.Count)
		{
			if (distTriggerList[i].Key >= distToTarget)
			{
				ProcessSingleMissileCommand(distTriggerList[i].Value);
				distTriggerList.RemoveAt(i);
			}
			else
			{
				i++;
			}
		}
	}

	if (timeTriggerList != null && timeTriggerList.Count > 0)
	{
		int i = 0;
		while (i < timeTriggerList.Count)
		{
			if (timeTriggerList[i].Key <= clock)
			{
				ProcessSingleMissileCommand(timeTriggerList[i].Value);
				timeTriggerList.RemoveAt(i);
			}
			else
			{
				i++;
			}
		}
	}
}

void CheckProximitySensors()
{
	int curfailunsafe = 0;


	for (int i = 0; i < proximitySensors.Count; i++)
	{
		ProximitySensor sensor = proximitySensors[i];
		if (sensor.lidar == null) continue;

		double dist = sensor.distance;
		if (dist <= 0)
		{
			dist = speed / SECOND * delta;
		}

		if (sensor.lidar.IsWorking && sensor.lidar.CanScan(dist))
		{
			if (sensor.dmsrange > dist && sensor.lidar.CanScan(sensor.dmsrange))
			{
				MyDetectedEntityInfo detected = sensor.lidar.Raycast(sensor.dmsrange, sensor.pitch, sensor.yaw);
				if (IsValidProximityTarget(ref detected))
				{
					double raycastDist = Vector3D.Distance((detected.HitPosition != null ? detected.HitPosition.Value : detected.Position), sensor.lidar.GetPosition());
					if (raycastDist <= dist)
					{
						ProcessMissileCommand(sensor.proximityTriggerCommands);
						sensor.lidar = null;
						return;
					}

					if (detected.HitPosition != null)
					{
						raycastDist = Vector3D.Distance(detected.Position, sensor.lidar.GetPosition());
					}

					if (sensor.dmsActive)
					{
						if (raycastDist > sensor.dmsPrevDist)
						{
							ProcessMissileCommand(sensor.proximityTriggerCommands);
							sensor.lidar = null;
							return;
						}
						sensor.dmsPrevDist = raycastDist;
					}
					else
					{
						sensor.dmsPrevDist = raycastDist;
						sensor.dmsActive = true;
					}
				}
				else if (sensor.dmsActive)
				{
					ProcessMissileCommand(sensor.proximityTriggerCommands);
					sensor.lidar = null;
					return;
				}
			}
			else
			{
				MyDetectedEntityInfo detected = sensor.lidar.Raycast(dist, sensor.pitch, sensor.yaw);
				if (IsValidProximityTarget(ref detected))
				{
					ProcessMissileCommand(sensor.proximityTriggerCommands);
					sensor.lidar = null;
					return;
				}
			}
		}
		else if (sensor.dmsActive)
		{
			ProcessMissileCommand(sensor.proximityTriggerCommands);
			sensor.lidar = null;
			return;
		}
		else if (sensor.failunsafe)
		{
			if (failunsafeGrpCnt > 0)
			{
				curfailunsafe++;
			}
			else
			{
				ProcessMissileCommand(sensor.failunsafeTriggerCommands);
				sensor.lidar = null;
				return;
			}
		}
	}

	if (failunsafeGrpCnt > 0 && curfailunsafe >= failunsafeGrpCnt)
	{
		string failunsafeCmd = null;
		for (int i = 0; i < proximitySensors.Count; i++)
		{
			ProximitySensor sensor = proximitySensors[i];
			if (sensor.lidar != null && sensor.failasgroup && sensor.failunsafe)
			{
				failunsafeCmd = sensor.failunsafeTriggerCommands;
				sensor.lidar = null;
			}
		}

		if (failunsafeCmd != null)
		{
			ProcessMissileCommand(failunsafeCmd);
			return;
		}
	}
}

void ProcessCommunicationMessage(string message)
{
	string[] msgTokens = message.Split(new char[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);

	for (int i = 0; i < msgTokens.Length; i++)
	{
		string msg = msgTokens[i];

		string recipient;
		string sender;
		string options;

		int start = msg.IndexOf("MSG;", 0, StringComparison.OrdinalIgnoreCase);
		if (start > -1)
		{
			start += 4;

			recipient = NextToken(msg, ref start, ';');
			sender = NextToken(msg, ref start, ';');
			options = NextToken(msg, ref start, ';');

			if (IsValidRecipient(recipient) && IsValidSender(sender))
			{
				if (msg.Length > start)
				{
					ProcessMissileCommand(msg.Substring(start));
				}
			}
		}
	}
}

bool IsValidRecipient(string recipient)
{
	if (recipient.Length == 0)
	{
		return true;
	}

	int code = (recipient[0] == '*' ? 1 : 0) + (recipient[recipient.Length - 1] == '*' ? 2 : 0);
	switch (code)
	{
	case 0:
		return missileId.Equals(recipient, StringComparison.OrdinalIgnoreCase) ||
		(missileGroup != null && missileGroup.Equals(recipient, StringComparison.OrdinalIgnoreCase));
	case 1:
		return missileId.EndsWith(recipient.Substring(1), StringComparison.OrdinalIgnoreCase) ||
		(missileGroup != null && missileGroup.EndsWith(recipient.Substring(1), StringComparison.OrdinalIgnoreCase));
	case 2:
		return missileId.StartsWith(recipient.Substring(0, recipient.Length - 1), StringComparison.OrdinalIgnoreCase) ||
		(missileGroup != null && missileGroup.StartsWith(recipient.Substring(0, recipient.Length - 1), StringComparison.OrdinalIgnoreCase));
	default:
		return (recipient.Length == 1) || (missileId.IndexOf(recipient.Substring(1, recipient.Length - 2), StringComparison.OrdinalIgnoreCase) > -1) ||
		(missileGroup != null && (missileGroup.IndexOf(recipient.Substring(1, recipient.Length - 2), StringComparison.OrdinalIgnoreCase) > -1));
	}
}

bool IsValidSender(string sender)
{
	if (allowedSenderId == null || allowedSenderId.Length == 0)
	{
		return true;
	}

	int code = (allowedSenderId[0] == '*' ? 1 : 0) + (allowedSenderId[allowedSenderId.Length - 1] == '*' ? 2 : 0);
	switch (code)
	{
	case 0:
		return sender.Equals(allowedSenderId, StringComparison.OrdinalIgnoreCase);
	case 1:
		return sender.EndsWith(allowedSenderId.Substring(1), StringComparison.OrdinalIgnoreCase);
	case 2:
		return sender.StartsWith(allowedSenderId.Substring(0, allowedSenderId.Length - 1), StringComparison.OrdinalIgnoreCase);
	default:
		return (allowedSenderId.Length == 1) || (sender.IndexOf(allowedSenderId.Substring(1, allowedSenderId.Length - 2), StringComparison.OrdinalIgnoreCase) > -1);
	}
}

string NextToken(string line, ref int start, char delim)
{
	if (line.Length > start)
	{
		int end = line.IndexOf(delim, start);
		if (end > -1)
		{
			string result = line.Substring(start, end - start);
			start = end + 1;
			return result;
		}
	}
	start = line.Length;
	return "";
}

void ProcessMissileCommand(string commandLine)
{
	string[] keyValues = commandLine.Split(',');

	for (int i = 0; i < keyValues.Length; i++)
	{
		string[] tokens = keyValues[i].Trim().Split(':');
		if (tokens.Length > 0)
		{
			ProcessSingleMissileCommand(tokens);
		}
	}
}

void ProcessSingleMissileCommand(string[] tokens)
{
	string cmdToken = tokens[0].Trim().ToUpper();
	if (cmdToken.Equals("GPS"))
	{
		if (tokens.Length >= 4)
		{
			Vector3D parsedVector;
			if (ParseVector(tokens, out parsedVector, (tokens.Length == 4 ? 1 : 2)))
			{
				commsPosition = parsedVector;
				commsPositionSet = true;
			}
		}
	}
	else if (cmdToken.Equals("FWD"))
	{
		if (tokens.Length >= 4)
		{
			Vector3D parsedVector1;
			if (ParseVector(tokens, out parsedVector1, 1))
			{
				Vector3D parsedVector2;
				if (tokens.Length >= 7)
				{
					if (!ParseVector(tokens, out parsedVector2, 4))
					{
						parsedVector2 = new Vector3D();
					}
				}
				else
				{
					parsedVector2 = new Vector3D();
				}
				commsFwd = new RayD(parsedVector2, parsedVector1);
				commsFwdSet = true;
			}
		}
	}
	else if (cmdToken.Equals("LDR"))
	{
		if (tokens.Length >= 2)
		{
			long entityId;
			if (!long.TryParse(tokens[1], out entityId))
			{
				entityId = -1;
			}

			Vector3D position;
			if (!(tokens.Length >= 5 && ParseVector(tokens, out position, 2)))
			{
				position = new Vector3D();
			}

			Vector3D velocity;
			if (!(tokens.Length >= 8 && ParseVector(tokens, out velocity, 5)))
			{
				velocity = new Vector3D();
			}

			Vector3D hitPosition;
			if (!(tokens.Length >= 11 && ParseVector(tokens, out hitPosition, 8)))
			{
				hitPosition = position;
			}

			Vector3D boxMin;
			if (!(tokens.Length >= 14 && ParseVector(tokens, out boxMin, 11)))
			{
				boxMin = position + new Vector3D(-1.25, -1.25, -1.25);
			}

			MatrixD orientation;
			if (!(tokens.Length >= 23 && ParseMatrix(tokens, out orientation, 14, true)))
			{
				orientation = new MatrixD();
			}

			int value;
			MyDetectedEntityType targetType;
			if (!(tokens.Length >= 24 && !int.TryParse(tokens[23], out value)))
			{
				value = 3;
			}
			try { targetType = (MyDetectedEntityType)value; }
			catch { targetType = MyDetectedEntityType.LargeGrid; }

			VRage.Game.MyRelationsBetweenPlayerAndBlock targetRelationship;
			if (!(tokens.Length >= 25 && !int.TryParse(tokens[24], out value)))
			{
				value = 3;
			}
			try { targetRelationship = (VRage.Game.MyRelationsBetweenPlayerAndBlock)value; }
			catch { targetRelationship = VRage.Game.MyRelationsBetweenPlayerAndBlock.Neutral; }

			long timestamp;
			if (!(tokens.Length >= 26 && !long.TryParse(tokens[25], out timestamp)))
			{
				timestamp = DateTime.Now.Ticks;
			}
			try { targetRelationship = (VRage.Game.MyRelationsBetweenPlayerAndBlock)value; }
			catch { targetRelationship = VRage.Game.MyRelationsBetweenPlayerAndBlock.Neutral; }

			BoundingBoxD boundingBox = new BoundingBoxD(boxMin, position + position - boxMin);

			commsLidarTarget = new MyDetectedEntityInfo(entityId, (tokens.Length >= 27 ? tokens[26] : ""), targetType, hitPosition, orientation, velocity, targetRelationship, boundingBox, timestamp);
			commsLidarTargetSet = true;
		}
	}
	else if (cmdToken.Equals("LOCK"))
	{
		homingReleaseLock = true;
	}
	else if (cmdToken.Equals("ABORT"))
	{
		ZeroTurnGyro();

		mode = 99;
	}
	else if (cmdToken.Equals("SEVER"))
	{
		if (shipRefLidars != null)
		{
			shipRefLidars.Clear();
		}

		shipRefFwd = null;
		shipRefPanel = null;

		enableMissileCommand = false;
	}
	else if (cmdToken.Equals("CRUISE"))
	{
		if (verticalTakeoff)
		{
			FireThrusters(launchThrusters, false);
			FireThrusters(thrusters, true);
		}

		ResetGyro();
		SetGyroOverride(true);

		if (spinAmount > 0)
		{
			SetGyroRoll(spinAmount);
		}

		lastTargetPosition = targetPosition = GetFlyStraightVector();

		subCounter = 0;
		subMode = 0;
		mode = 4;
	}
	else if (cmdToken.Equals("GLIDE"))
	{
		if (verticalTakeoff)
		{
			FireThrusters(launchThrusters, false);
			FireThrusters(thrusters, true);
		}

		ResetGyro();
		SetGyroOverride(true);

		if (spinAmount > 0)
		{
			SetGyroRoll(spinAmount);
		}


		if (mode == -1)
		{
			refFwdPosition = midPoint;
			refFwdVector = Vector3D.Normalize(driftVector);
			refFwdSet = true;
		}
		else if (mode == 8 && homingTurret != null)
		{
			refFwdPosition = midPoint;
			refFwdVector = CalculateTurretViewVector(homingTurret);
			refFwdSet = true;
		}
		else if ((mode == 2 && subMode == 0) || (mode == 3))
		{
			if (commsFwdSet)
			{
				commsFwdSet = false;

				refFwdPosition = commsFwd.Position;
				refFwdVector = commsFwd.Direction;
				refFwdSet = true;
			}
			else if (shipRefFwd != null)
			{
				refFwdPosition = shipRefFwd.WorldMatrix.Translation;
				refFwdVector = (fwdIsTurret ? CalculateTurretViewVector(shipRefFwd as IMyLargeTurretBase) : shipRefFwd.WorldMatrix.Forward);
				refFwdSet = true;
			}
		}
		else
		{
			refFwdPosition = midPoint;
			refFwdVector = Vector3D.Normalize(targetPosition - midPoint);
			refFwdSet = true;
		}

		subCounter = 0;
		subMode = 0;
		mode = 6;
	}
	else if (cmdToken.StartsWith("ACT") && tokens.Length >= 3)
	{
		char opCode = (cmdToken.Length >= 4 ? cmdToken[3] : 'B');
		List<IMyTerminalBlock> triggerBlocks = null;
		switch (opCode)
		{
		case 'B':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 3);
			break;
		case 'P':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 1);
			break;
		case 'S':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 2);
			break;
		case 'W':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 0);
			break;
		}

		if (triggerBlocks != null)
		{
			for (int i = 0; i < triggerBlocks.Count; i++)
			{
				ITerminalAction action = triggerBlocks[i].GetActionWithName(tokens[2]);
				if (action != null)
				{
					action.Apply(triggerBlocks[i]);
				}
			}
		}
	}
	else if (cmdToken.StartsWith("SET") && tokens.Length >= 3)
	{
		char opCode = (cmdToken.Length >= 4 ? cmdToken[3] : 'B');
		List<IMyTerminalBlock> triggerBlocks = null;
		switch (opCode)
		{
		case 'B':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 3);
			break;
		case 'P':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 1);
			break;
		case 'S':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 2);
			break;
		case 'W':
			triggerBlocks = GetBlocksWithName<IMyTerminalBlock>(tokens[1], 0);
			break;
		}

		char propCode = (cmdToken.Length >= 5 ? cmdToken[4] : 'P');

		if (triggerBlocks != null)
		{
			for (int i = 0; i < triggerBlocks.Count; i++)
			{
				switch (propCode)
				{
				case 'P':
					triggerBlocks[i].SetValueFloat(tokens[2], float.Parse(tokens[3]));
					break;
				case 'B':
					triggerBlocks[i].SetValueBool(tokens[2], bool.Parse(tokens[3]));
					break;
				case 'D':
					triggerBlocks[i].SetValueFloat(tokens[2], (float)distToTarget / float.Parse(tokens[3]));
					break;
				case 'S':
					triggerBlocks[i].SetValueFloat(tokens[2], (float)speed / float.Parse(tokens[3]));
					break;
				case 'T':
					triggerBlocks[i].SetValueFloat(tokens[2], (float)(distToTarget / speed) / float.Parse(tokens[3]));
					break;
				case 'A':
					triggerBlocks[i].SetValueFloat(tokens[2], triggerBlocks[i].GetValueFloat(tokens[2]) + float.Parse(tokens[3]));
					break;
				case 'M':
					triggerBlocks[i].SetValueFloat(tokens[2], triggerBlocks[i].GetValueFloat(tokens[2]) * float.Parse(tokens[3]));
					break;
				}
			}
		}
	}
	else if (cmdToken.Equals("SPIN") && tokens.Length >= 1)
	{
		SetGyroRoll(tokens.Length >= 2 ? Int32.Parse(tokens[1]) : 30);
	}
}

void ExecuteTriggerCommand(string commandLine)
{
	int startIndex = commandLine.IndexOf('[') + 1;
	int endIndex = commandLine.LastIndexOf(']');

	string command = (startIndex > 0 && endIndex > -1 ? commandLine.Substring(startIndex, endIndex - startIndex) : commandLine);
	string[] keyValues = command.Split(',');

	for (int i = 0; i < keyValues.Length; i++)
	{
		string[] tokens = keyValues[i].Trim().Split(':');
		if (tokens.Length > 0)
		{
			string cmdToken = tokens[0].Trim();
			if (cmdToken.Equals("TGR") && tokens.Length >= 3)
			{
				if (rpmTriggerList == null)
				{
					rpmTriggerList = new List<KeyValuePair<double, string[]>>();
				}

				string[] items = new string[tokens.Length - 2];
				Array.Copy(tokens, 2, items, 0, items.Length);
				rpmTriggerList.Add(new KeyValuePair<double, string[]>(Double.Parse(tokens[1]), items));

				haveTriggerCommands = true;
			}
			else if (cmdToken.Equals("TGD") && tokens.Length >= 3)
			{
				if (distTriggerList == null)
				{
					distTriggerList = new List<KeyValuePair<double, string[]>>();
				}

				string[] items = new string[tokens.Length - 2];
				Array.Copy(tokens, 2, items, 0, items.Length);
				distTriggerList.Add(new KeyValuePair<double, string[]>(Double.Parse(tokens[1]), items));

				haveTriggerCommands = true;
			}
			else if (cmdToken.Equals("TGE") && tokens.Length >= 3)
			{
				if (distTriggerList == null)
				{
					distTriggerList = new List<KeyValuePair<double, string[]>>();
				}

				string[] items = new string[tokens.Length - 2];
				Array.Copy(tokens, 2, items, 0, items.Length);
				distTriggerList.Add(new KeyValuePair<double, string[]>(distToTarget - Double.Parse(tokens[1]), items));

				haveTriggerCommands = true;
			}
			else if (cmdToken.Equals("TGT") && tokens.Length >= 3)
			{
				if (timeTriggerList == null)
				{
					timeTriggerList = new List<KeyValuePair<long, string[]>>();
				}

				string[] items = new string[tokens.Length - 2];
				Array.Copy(tokens, 2, items, 0, items.Length);
				long ticks = (long)(Double.Parse(tokens[1]) * SECOND) + clock;
				timeTriggerList.Add(new KeyValuePair<long, string[]>(ticks, items));

				haveTriggerCommands = true;
			}
			else
			{
				ProcessSingleMissileCommand(tokens);
			}
		}
	}
}

//------------------------------ Script Debugging Methods ------------------------------

void CheckMissile()
{
	Echo("----- Missile Issues -----\n");

	shipRefLidars = new List<IMyCameraBlock>(1);
	InitMissileBlocks(true);

	Echo("\n----- Missile Parameters -----");

	Echo("\n[Compatible Homing Modes]:");
	Echo((missileLidars.Count > 0 ? "0,1,2,3,4,5,6,7" : "3,4,6,7") + (homingTurret != null ? ",8" : ""));
	Echo("\nDrift Compensation: " + (boolDrift == true ? "Yes" : "No"));
	Echo("Gravity Dampeners: " + (boolNaturalDampener == true ? "Yes" : "No"));
	Echo("Target Leading: " + (boolLeadTarget == true ? "Yes" : "No"));
	Echo("\nVertical Takeoff: " + (verticalTakeoff == true ? "Yes" : "No"));
	Echo("\nProximity Sensors: " + (proximitySensors != null ? "Yes" : "No"));
	Echo("\n<<Below lists the Detected Blocks.\nSet to Show On HUD for checking>>");

	Echo("\nOne of the Forward Thrusters:");
	if (thrusters.Count > 0)
	{
		Echo(thrusters[0].CustomName);
		thrusters[0].ShowOnHUD = true;
	}
	else
	{
		Echo("<NONE>");
	}

	Echo("\nOne of the Gravity Dampeners:");
	if (refDwdBlock != null)
	{
		Echo(refDwdBlock.CustomName);
		refDwdBlock.ShowOnHUD = true;
	}
	else
	{
		Echo("<NONE>");
	}

	if (thrusters.Count > 0)
	{
		bool haveFwdLidars = false;
		for (int i = 0; i < missileLidars.Count; i++)
		{
			if (missileLidars[i].WorldMatrix.Forward.Dot(thrusters[0].WorldMatrix.Backward) > 0.99)
			{
				haveFwdLidars = true;
				break;
			}
		}
		if (!haveFwdLidars)
		{
			Echo("\nWarning: Missing Forward Facing Cameras.");
		}
	}

	Echo("\n--- End Of Check, Recompile Script & Remove CHECK Argument ---");
}

void CheckLaunchingShip()
{
	Echo("----- Launching Ship Warnings -----\n");

	InitLaunchingShipRefBlocks(true);

	Echo("\n----- Launching Ship Parameters -----");

	Echo("\n<<Below lists the Detected Blocks.\nSet to Show On HUD for checking>>");

	Echo("\nR_FORWARD Aiming Block:");
	if (shipRefFwd != null)
	{
		Echo(shipRefFwd.CustomName);
		shipRefFwd.ShowOnHUD = true;
	}
	else
	{
		Echo("<NONE>");
	}

	Echo("\nOne of the R_LIDAR Cameras:");
	if (shipRefLidars.Count > 0)
	{
		Echo(shipRefLidars[0].CustomName);
		shipRefLidars[0].ShowOnHUD = true;
	}
	else
	{
		Echo("<NONE>");
	}

	Echo("\nR_TARGET GPS Text Panel:");
	if (shipRefPanel != null)
	{
		Echo(shipRefPanel.CustomName);
		shipRefPanel.ShowOnHUD = true;
	}
	else
	{
		Echo("<NONE>");
	}

	Echo("\nLock-On Alert Sound Block:");
	if (alertBlock != null)
	{
		Echo(alertBlock.CustomName);
		alertBlock.ShowOnHUD = true;
	}
	else
	{
		Echo("<NONE>");
	}

	DetachFromGrid(true);

	Echo("\nBlock to Detach on Launch:");
	if (detachBlock != null)
	{
		Echo(detachBlock.CustomName);
		detachBlock.ShowOnHUD = true;
	}
	else
	{
		Echo("<WARNING NOT FOUND>");
	}

	Echo("\n--- End Of Check, Recompile Script & Remove CHECKSHIP Argument ---");
}

//------------------------------ Initialization Methods ------------------------------

bool InitLaunchingShipRefBlocks(bool testOnly = false)
{
	List<IMyTerminalBlock> blocks;

	blocks = GetBlocksWithName<IMyCameraBlock>(strShipRefLidar);
	if (blocks.Count == 0)
	{
		Echo("Warning: Missing Camera Lidars with tag " + strShipRefLidar);

		shipRefLidars = new List<IMyCameraBlock>(1);
	}
	else
	{
		shipRefLidars = new List<IMyCameraBlock>(blocks.Count);

		for (int i = 0; i < blocks.Count; i++)
		{
			IMyCameraBlock refLidar = blocks[i] as IMyCameraBlock;

			if (!testOnly)
			{
				refLidar.ApplyAction("OnOff_On");
				refLidar.EnableRaycast = true;
			}

			shipRefLidars.Add(refLidar);
		}
	}

	blocks = GetBlocksWithName<IMyTextPanel>(strShipRefPanel);
	if (blocks.Count == 0)
	{
		Echo("Warning: Missing Text Panel with tag " + strShipRefPanel);
	}
	else
	{
		if (blocks.Count > 1)
		{
			Echo("Warning: Multiple Text Panel with tag " + strShipRefPanel + " found. Using first one detected.");
		}

		shipRefPanel = blocks[0] as IMyTextPanel;
	}

	if (shipRefPanel == null)
	{
		enableMissileCommand = false;
	}

	blocks = GetBlocksWithName<IMyTerminalBlock>(strShipRefFwd);
	if (blocks.Count == 0)
	{
		blocks = GetBlocksOfType<IMyCockpit>();

		if (blocks.Count == 0)
		{
			Echo("Warning: Missing Forward Block with tag " + strShipRefFwd);
		}
		else
		{
			Echo("Warning: Missing Forward Block, Using Main/Available Cockpit instead");

			for (int i = 0; i < blocks.Count; i++)
			{
				shipRefFwd = blocks[i];

				if (shipRefFwd.GetValueBool("MainCockpit"))
				{
					break;
				}
			}
		}
	}
	else
	{
		if (blocks.Count > 1)
		{
			Echo("Warning: Multiple Forward Block with tag " + strShipRefFwd + " found. Using first one detected.");
		}

		shipRefFwd = blocks[0];
	}

	fwdIsTurret = ((shipRefFwd as IMyLargeTurretBase) != null);

	alertBlock = GetSingleBlockWithName(strLockTriggerBlockTag);

	notMissile = GetSingleBlockWithName(strShipRefNotMissileTag);

	return true;
}

bool InitMissileBlocks(bool testOnly = false)
{
	gyros = GetGyroscopes();

	thrusters = GetThrusters();

	remoteControl = GetRemoteControl();

	missileLidars = GetLidars();

	if (missileBlockSameGridOnly)
	{
		FilterSameGrid(Me.CubeGrid, ref gyros);
		FilterSameGrid(Me.CubeGrid, ref thrusters);
		FilterSameGrid(Me.CubeGrid, ref missileLidars);
	}

	homingTurret = GetHomingTurret();
	if (homingTurret != null && !testOnly)
	{
		homingTurret.EnableIdleRotation = false;
	}

	proximitySensors = null;
	failunsafeGrpCnt = 0;

	for (int i = 0; i < missileLidars.Count; i++)
	{
		if (!testOnly)
		{
			missileLidars[i].ApplyAction("OnOff_On");
			missileLidars[i].EnableRaycast = true;
		}

		int startIndex = missileLidars[i].CustomName.IndexOf(strProximitySensorTag, StringComparison.OrdinalIgnoreCase);
		if (startIndex > -1)
		{
			if (proximitySensors == null)
			{
				proximitySensors = new List<ProximitySensor>();
				hasProximitySensors = true;
			}

			ProximitySensor proxSensor = new ProximitySensor(missileLidars[i]);
			proximitySensors.Add(proxSensor);

			double proximityDist = 0;
			startIndex += strProximitySensorTag.Length + 1;
			if (missileLidars[i].CustomName.Length > startIndex)
			{
				if (missileLidars[i].CustomName[startIndex - 1] == '_')
				{
					string proximityDistStr;
					int endIndex = missileLidars[i].CustomName.IndexOf(" ", startIndex, StringComparison.OrdinalIgnoreCase);
					if (endIndex == -1)
					{
						proximityDistStr = missileLidars[i].CustomName.Substring(startIndex).Trim();
					}
					else
					{
						proximityDistStr = missileLidars[i].CustomName.Substring(startIndex, endIndex - startIndex).Trim();
					}

					if (!double.TryParse(proximityDistStr, out proximityDist))
					{
						proximityDist = 0;
					}
				}
			}
			proxSensor.distance = proximityDist;

			CustomConfiguration cfg = new CustomConfiguration(proxSensor.lidar);
			cfg.Load();

			float yaw = 0, pitch = 0;
			bool failunsafe = false, failasgroup = false;
			double distance = 0, dmsrange = 0;
			string failunsafeTriggerCmds = failunsafeTriggerCommands, proximityTriggerCmds = proximityTriggerCommands;

			cfg.Get("yaw", ref yaw);
			cfg.Get("pitch", ref pitch);
			cfg.Get("failunsafe", ref failunsafe);
			cfg.Get("failasgroup", ref failasgroup);
			cfg.Get("distance", ref distance);
			cfg.Get("dmsrange", ref dmsrange);
			cfg.Get("failunsafeTriggerCommands", ref failunsafeTriggerCmds);
			cfg.Get("proximityTriggerCommands", ref proximityTriggerCmds);

			proxSensor.yaw = yaw;
			proxSensor.pitch = pitch;
			proxSensor.failunsafe = failunsafe;
			proxSensor.failasgroup = failasgroup;
			proxSensor.distance = distance;
			proxSensor.dmsrange = dmsrange;
			proxSensor.failunsafeTriggerCommands = failunsafeTriggerCmds;
			proxSensor.proximityTriggerCommands = proximityTriggerCmds;

			if (failasgroup && failunsafe)
			{
				failunsafeGrpCnt++;
			}
		}
	}

	if (missileLidars.Count > 0 || shipRefLidars.Count > 0)
	{
		IMyCameraBlock lidar = (missileLidars.Count > 0 ? missileLidars[0] : shipRefLidars[0]);

		coneLimit = lidar.RaycastConeLimit;
		sideScale = Math.Tan((90 - coneLimit) * RADIAN_FACTOR);
		ticksRatio = lidar.TimeUntilScan(lidar.AvailableScanRange + 1000) * 10;
		ticksFactor = ticksRatio / Math.Max((int)Math.Floor(missileLidars.Count * LIDAR_REFRESH_CALC_FACTOR), 1);
	}
	else
	{
		coneLimit = 45;
		sideScale = 1;
		ticksRatio = 5000;
		ticksFactor = 5000;
	}

	bool isFixedDirection = false;

	if (strDirectionRefBlockTag != null && strDirectionRefBlockTag.Length > 0)
	{
		refFwdBlock = GetSingleBlockWithName(strDirectionRefBlockTag, missileBlockSameGridOnly);
		isFixedDirection = (refFwdBlock != null);
	}

	if (spinAmount > 0)
	{
		boolNaturalDampener = false;
	}

	if (refFwdBlock == null || boolNaturalDampener == null || boolDrift == null || verticalTakeoff)
	{
		thrustValues = ComputeMaxThrustValues(thrusters);
	}

	if (refFwdBlock == null)
	{
		refFwdBlock = ComputeHighestThrustReference(thrusters, thrustValues);
		refFwdReverse = true;
	}

	if (refFwdBlock == null)
	{
		Echo("Warning: Missing Reference Blocks or Forward Thrusters. Using " + (remoteControl == null ? "Programmable Block" : "Remote Control") + " for Reference.");

		refFwdBlock = (remoteControl == null ? Me : (IMyTerminalBlock)remoteControl);
		refFwdReverse = false;
	}

	refWorldMatrix = refFwdBlock.WorldMatrix;
	if (refFwdReverse)
	{
		refWorldMatrix = MatrixD.CreateWorld(refWorldMatrix.Translation, refWorldMatrix.Backward, refWorldMatrix.Up);
	}

	InitGyrosAndThrusters(isFixedDirection, testOnly);
	thrustValues = null;

	if (boolLeadTarget == null)
	{
		boolLeadTarget = true;
	}

	if (strStatusDisplayPrefix != null && strStatusDisplayPrefix.Length > 0)
	{
		List<IMyTerminalBlock> blocks = GetBlocksWithName<IMyTerminalBlock>(strStatusDisplayPrefix, 1);
		if (blocks.Count > 0)
		{
			statusDisplay = blocks[0];

			if (!testOnly)
			{
				if (statusDisplay.HasAction("OnOff_On"))
				{
					statusDisplay.ApplyAction("OnOff_On");

					IMyRadioAntenna radioAntenna = statusDisplay as IMyRadioAntenna;
					if (radioAntenna != null && !radioAntenna.IsBroadcasting)
					{
						radioAntenna.ApplyAction("EnableBroadCast");
					}
				}
			}
		}
	}

	return true;
}

List<IMyTerminalBlock> GetGyroscopes()
{
	List<IMyTerminalBlock> blocks = GetBlocksWithName<IMyGyro>(strGyroscopesTag);
	if (blocks.Count > 0)
	{
		return blocks;
	}

	GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks);
	if (blocks.Count == 0)
	{
		Echo("Error: Missing Gyroscopes.");
	}
	return blocks;
}

List<IMyTerminalBlock> GetThrusters()
{
	List<IMyTerminalBlock> blocks = GetBlocksWithName<IMyThrust>(strThrustersTag);
	if (blocks.Count > 0)
	{
		return blocks;
	}

	GridTerminalSystem.GetBlocksOfType<IMyThrust>(blocks);
	if (blocks.Count == 0)
	{
		Echo("Warning: Missing Thrusters.");
	}
	return blocks;
}

IMyShipController GetRemoteControl()
{
	List<IMyTerminalBlock> blocks = GetBlocksOfType<IMyShipController>();
	if (missileBlockSameGridOnly)
	{
		FilterSameGrid(Me.CubeGrid, ref blocks);
	}

	IMyShipController remoteBlock = (blocks.Count > 0 ? blocks[0] as IMyShipController : null);
	if (remoteBlock == null)
	{
		Echo("Error: Missing Remote Control.");
	}
	return remoteBlock;
}

List<IMyCameraBlock> GetLidars()
{
	return GetBlocksOfTypeCasted<IMyCameraBlock>();
}

IMyLargeTurretBase GetHomingTurret()
{
	List<IMyTerminalBlock> blocks = GetBlocksOfType<IMyLargeTurretBase>();
	if (missileBlockSameGridOnly)
	{
		FilterSameGrid(Me.CubeGrid, ref blocks);
	}

	IMyLargeTurretBase turret = (blocks.Count > 0 ? blocks[0] as IMyLargeTurretBase : null);
	return turret;
}

void InitGyrosAndThrusters(bool isFixedDirection, bool testOnly = false)
{
	//---------- Find Gyroscope Orientation With Respect To Ship ----------

	gyroYawField = new string[gyros.Count];
	gyroPitchField = new string[gyros.Count];
	gyroYawFactor = new float[gyros.Count];
	gyroPitchFactor = new float[gyros.Count];
	gyroRollField = new string[gyros.Count];
	gyroRollFactor = new float[gyros.Count];

	for (int i = 0; i < gyros.Count; i++)
	{
		Base6Directions.Direction gyroUp = gyros[i].WorldMatrix.GetClosestDirection(refWorldMatrix.Up);
		Base6Directions.Direction gyroLeft = gyros[i].WorldMatrix.GetClosestDirection(refWorldMatrix.Left);
		Base6Directions.Direction gyroFwd = gyros[i].WorldMatrix.GetClosestDirection(refWorldMatrix.Forward);

		SetRelativeDirection(ref gyroUp, i, ref gyroYawField, ref gyroYawFactor);
		SetRelativeDirection(ref gyroLeft, i, ref gyroPitchField, ref gyroPitchFactor);
		SetRelativeDirection(ref gyroFwd, i, ref gyroRollField, ref gyroRollFactor);

		if (!testOnly)
		{
			gyros[i].ApplyAction("OnOff_On");
		}
	}

	//---------- Check Whether To Use Default PID Values ----------

	if (useDefaultPIDValues)
	{
		if (Me.CubeGrid.ToString().Contains("Large"))
		{
			AIM_P = DEF_BIG_GRID_P;
			AIM_I = DEF_BIG_GRID_I;
			AIM_D = DEF_BIG_GRID_D;
		}
		else
		{
			AIM_P = DEF_SMALL_GRID_P;
			AIM_I = DEF_SMALL_GRID_I;
			AIM_D = DEF_SMALL_GRID_D;
			AIM_LIMIT *= 2;
		}
	}

	//---------- Find Forward Thrusters ----------

	List<IMyTerminalBlock> checkThrusters = thrusters;
	thrusters = new List<IMyTerminalBlock>();

	if (!isFixedDirection || boolNaturalDampener == null || boolDrift == null || verticalTakeoff)
	{
		IMyTerminalBlock leftThruster = null;
		IMyTerminalBlock rightThruster = null;
		IMyTerminalBlock upThruster = null;
		IMyTerminalBlock downThruster = null;

		float leftThrustTotal = 0;
		float rightThrustTotal = 0;
		float upThrustTotal = 0;
		float downThrustTotal = 0;

		for (int i = 0; i < checkThrusters.Count; i++)
		{
			Base6Directions.Direction thrusterDirection = refWorldMatrix.GetClosestDirection(checkThrusters[i].WorldMatrix.Backward);
			switch (thrusterDirection)
			{
			case Base6Directions.Direction.Forward:
				thrusters.Add(checkThrusters[i]);
				break;
			case Base6Directions.Direction.Left:
				leftThruster = checkThrusters[i];
				leftThrustTotal += thrustValues[i];
				break;
			case Base6Directions.Direction.Right:
				rightThruster = checkThrusters[i];
				rightThrustTotal += thrustValues[i];
				break;
			case Base6Directions.Direction.Up:
				upThruster = checkThrusters[i];
				upThrustTotal += thrustValues[i];
				if (isFixedDirection)
				{
					refDwdBlock = upThruster;
				}
				break;
			case Base6Directions.Direction.Down:
				downThruster = checkThrusters[i];
				downThrustTotal += thrustValues[i];
				break;
			}

			if (!testOnly)
			{
				checkThrusters[i].ApplyAction("OnOff_On");
			}
		}

		float highestThrust = Math.Max(Math.Max(Math.Max(leftThrustTotal, rightThrustTotal), upThrustTotal), downThrustTotal);
		if (highestThrust == 0)
		{
			if (boolNaturalDampener == true)
			{
				Echo("Warning: Natural Gravity Dampener feature not possible as there are no Downward Thrusters found.");
			}
			boolNaturalDampener = false;

			if (boolDrift == null)
			{
				boolDrift = true;
			}
		}
		else
		{
			if (!isFixedDirection)
			{
				if (leftThrustTotal == highestThrust)
				{
					refDwdBlock = leftThruster;
				}
				else if (rightThrustTotal == highestThrust)
				{
					refDwdBlock = rightThruster;
				}
				else if (upThrustTotal == highestThrust)
				{
					refDwdBlock = upThruster;
				}
				else
				{
					refDwdBlock = downThruster;
				}
			}
			boolNaturalDampener = (refDwdBlock != null);

			if (boolDrift == null)
			{
				float lowestThrust = Math.Min(Math.Min(Math.Min(leftThrustTotal, rightThrustTotal), upThrustTotal), downThrustTotal);
				boolDrift = (highestThrust > lowestThrust * 2);
			}
		}

		if (verticalTakeoff && refDwdBlock != null)
		{
			launchThrusters = new List<IMyTerminalBlock>();

			for (int i = 0; i < checkThrusters.Count; i++)
			{
				if (refDwdBlock.WorldMatrix.Forward.Dot(checkThrusters[i].WorldMatrix.Forward) >= 0.9)
				{
					launchThrusters.Add(checkThrusters[i]);
				}
			}
		}
	}
	else
	{
		for (int i = 0; i < checkThrusters.Count; i++)
		{
			Base6Directions.Direction thrusterDirection = refWorldMatrix.GetClosestDirection(checkThrusters[i].WorldMatrix.Backward);
			if (thrusterDirection == Base6Directions.Direction.Forward)
			{
				thrusters.Add(checkThrusters[i]);
			}
			else if (boolNaturalDampener == true && thrusterDirection == Base6Directions.Direction.Up)
			{
				refDwdBlock = checkThrusters[i];
			}

			if (!testOnly)
			{
				checkThrusters[i].ApplyAction("OnOff_On");
			}
		}

		if (boolNaturalDampener == true && refDwdBlock == null)
		{
			Echo("Warning: Natural Gravity Dampener feature not possible as there are no Downward Thrusters found.");
			boolNaturalDampener = false;
		}
	}
}

void SetRelativeDirection(ref Base6Directions.Direction dir, int i, ref string[] field, ref float[] factor)
{
	switch (dir)
	{
	case Base6Directions.Direction.Up:
		field[i] = "Yaw";
		factor[i] = GYRO_FACTOR;
		break;
	case Base6Directions.Direction.Down:
		field[i] = "Yaw";
		factor[i] = -GYRO_FACTOR;
		break;
	case Base6Directions.Direction.Left:
		field[i] = "Pitch";
		factor[i] = GYRO_FACTOR;
		break;
	case Base6Directions.Direction.Right:
		field[i] = "Pitch";
		factor[i] = -GYRO_FACTOR;
		break;
	case Base6Directions.Direction.Forward:
		field[i] = "Roll";
		factor[i] = -GYRO_FACTOR;
		break;
	case Base6Directions.Direction.Backward:
		field[i] = "Roll";
		factor[i] = GYRO_FACTOR;
		break;
	}
}

float[] ComputeMaxThrustValues(List<IMyTerminalBlock> checkThrusters)
{
	float[] thrustValues = new float[checkThrusters.Count];

	for (int i = 0; i < checkThrusters.Count; i++)
	{
		thrustValues[i] = Math.Max(((IMyThrust)checkThrusters[i]).MaxEffectiveThrust, 0.00001f);
	}

	return thrustValues;
}

IMyTerminalBlock ComputeHighestThrustReference(List<IMyTerminalBlock> checkThrusters, float[] thrustValues)
{
	if (checkThrusters.Count == 0)
	{
		return null;
	}

	IMyTerminalBlock fwdThruster = null;
	IMyTerminalBlock bwdThruster = null;
	IMyTerminalBlock leftThruster = null;
	IMyTerminalBlock rightThruster = null;
	IMyTerminalBlock upThruster = null;
	IMyTerminalBlock downThruster = null;

	float fwdThrustTotal = 0;
	float bwdThrustTotal = 0;
	float leftThrustTotal = 0;
	float rightThrustTotal = 0;
	float upThrustTotal = 0;
	float downThrustTotal = 0;

	for (int i = 0; i < checkThrusters.Count; i++)
	{
		Base6Directions.Direction thrusterDirection = Me.WorldMatrix.GetClosestDirection(checkThrusters[i].WorldMatrix.Backward);
		switch (thrusterDirection)
		{
		case Base6Directions.Direction.Forward:
			fwdThruster = checkThrusters[i];
			fwdThrustTotal += thrustValues[i];
			break;
		case Base6Directions.Direction.Backward:
			bwdThruster = checkThrusters[i];
			bwdThrustTotal += thrustValues[i];
			break;
		case Base6Directions.Direction.Left:
			leftThruster = checkThrusters[i];
			leftThrustTotal += thrustValues[i];
			break;
		case Base6Directions.Direction.Right:
			rightThruster = checkThrusters[i];
			rightThrustTotal += thrustValues[i];
			break;
		case Base6Directions.Direction.Up:
			upThruster = checkThrusters[i];
			upThrustTotal += thrustValues[i];
			break;
		case Base6Directions.Direction.Down:
			downThruster = checkThrusters[i];
			downThrustTotal += thrustValues[i];
			break;
		}
	}

	List<IMyTerminalBlock> highestThrustReferences = new List<IMyTerminalBlock>(2);

	float highestThrust = Math.Max(Math.Max(Math.Max(Math.Max(Math.Max(fwdThrustTotal, bwdThrustTotal), leftThrustTotal), rightThrustTotal), upThrustTotal), downThrustTotal);
	if (fwdThrustTotal == highestThrust && fwdThruster != null)
	{
		highestThrustReferences.Add(fwdThruster);
	}
	if (bwdThrustTotal == highestThrust && bwdThruster != null)
	{
		highestThrustReferences.Add(bwdThruster);
	}
	if (leftThrustTotal == highestThrust && leftThruster != null)
	{
		highestThrustReferences.Add(leftThruster);
	}
	if (rightThrustTotal == highestThrust && rightThruster != null)
	{
		highestThrustReferences.Add(rightThruster);
	}
	if (upThrustTotal == highestThrust && upThruster != null)
	{
		highestThrustReferences.Add(upThruster);
	}
	if (downThrustTotal == highestThrust && downThruster != null)
	{
		highestThrustReferences.Add(downThruster);
	}

	if (highestThrustReferences.Count == 1)
	{
		return highestThrustReferences[0];
	}
	else
	{
		Vector3D diagonalVector = ComputeBlockGridDiagonalVector(Me);

		IMyTerminalBlock closestToLengthRef = highestThrustReferences[0];
		double closestToLengthValue = 0;

		for (int i = 0; i < highestThrustReferences.Count; i++)
		{
			double dotLength = Math.Abs(diagonalVector.Dot(highestThrustReferences[i].WorldMatrix.Forward));
			if (dotLength > closestToLengthValue)
			{
				closestToLengthValue = dotLength;
				closestToLengthRef = highestThrustReferences[i];
			}
		}

		return closestToLengthRef;
	}
}

Vector3D ComputeBlockGridDiagonalVector(IMyTerminalBlock block)
{
	IMyCubeGrid cubeGrid = block.CubeGrid;

	Vector3D minVector = cubeGrid.GridIntegerToWorld(cubeGrid.Min);
	Vector3D maxVector = cubeGrid.GridIntegerToWorld(cubeGrid.Max);

	return (minVector - maxVector);
}

Vector3D ComputeBlockGridMidPoint(IMyTerminalBlock block)
{
	return (block.CubeGrid.GridIntegerToWorld(block.CubeGrid.Min) + block.CubeGrid.GridIntegerToWorld(block.CubeGrid.Max)) / 2;
}

//------------------------------ Thruster Control Methods ------------------------------

void FireThrusters(List<IMyTerminalBlock> listThrusters, bool overrideMode)
{
	if (listThrusters != null)
	{
		for (int i = 0; i < listThrusters.Count; i++)
		{
			listThrusters[i].SetValue("Override", (overrideMode ? listThrusters[i].GetMaximum<float>("Override") : 0f));
		}
	}
}

//------------------------------ Gyroscope Control Methods ------------------------------

void SetGyroOverride(bool bOverride)
{
	for (int i = 0; i < gyros.Count; i++)
	{
		if (((IMyGyro)gyros[i]).GyroOverride != bOverride)
		{
			gyros[i].ApplyAction("Override");
		}
	}
}

void SetGyroYaw(double yawRate)
{
	for (int i = 0; i < gyros.Count; i++)
	{
		gyros[i].SetValue(gyroYawField[i], (float)yawRate * gyroYawFactor[i]);
	}
}

void SetGyroPitch(double pitchRate)
{
	for (int i = 0; i < gyros.Count; i++)
	{
		gyros[i].SetValue(gyroPitchField[i], (float)pitchRate * gyroPitchFactor[i]);
	}
}

void SetGyroRoll(double rollRate)
{
	for (int i = 0; i < gyros.Count; i++)
	{
		gyros[i].SetValue(gyroRollField[i], (float)rollRate * gyroRollFactor[i]);
	}
}

void ZeroTurnGyro()
{
	for (int i = 0; i < gyros.Count; i++)
	{
		gyros[i].SetValue(gyroYawField[i], 0f);
		gyros[i].SetValue(gyroPitchField[i], 0f);
	}
}

void ResetGyro()
{
	for (int i = 0; i < gyros.Count; i++)
	{
		gyros[i].SetValue("Yaw", 0f);
		gyros[i].SetValue("Pitch", 0f);
		gyros[i].SetValue("Roll", 0f);
	}
}

//------------------------------ Name Finder API ------------------------------

IMyTerminalBlock GetSingleBlockWithName(string name, bool sameGridOnly = false)
{
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName(name, blocks);
	if (sameGridOnly)
	{
		FilterSameGrid(Me.CubeGrid, ref blocks);
	}

	return (blocks.Count > 0 ? blocks[0] : null);
}

List<IMyTerminalBlock> GetBlocksOfType<T>() where T: class, IMyTerminalBlock
{
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocksOfType<T>(blocks);

	return blocks;
}

List<T> GetBlocksOfTypeCasted<T>() where T: class, IMyTerminalBlock
{
	List<T> blocks = new List<T>();
	GridTerminalSystem.GetBlocksOfType<T>(blocks);

	return blocks;
}

List<IMyTerminalBlock> GetBlocksWithName(string name)
{
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName(name, blocks);

	return blocks;
}

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

void FilterSameGrid<T>(IMyCubeGrid grid, ref List<T> blocks) where T: class, IMyTerminalBlock
{
	List<T> filtered = new List<T>();
	for (int i = 0; i < blocks.Count; i++)
	{
		if (blocks[i].CubeGrid == grid)
		{
			filtered.Add(blocks[i]);
		}
	}
	blocks = filtered;
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

public class VectorAverageFilter
{
	public int vectorIndex = 0;
	public Vector3D[] vectorArr = null;
	public Vector3D vectorSum = new Vector3D();

	public VectorAverageFilter(int size)
	{
		vectorArr = new Vector3D[size];
	}

	public void Filter(ref Vector3D vectorIn, out Vector3D vectorOut)
	{
		vectorSum -= vectorArr[vectorIndex];
		vectorArr[vectorIndex] = vectorIn;
		vectorSum += vectorArr[vectorIndex];
		vectorIndex++;
		if (vectorIndex >= vectorArr.Length)
		{
			vectorIndex = 0;
		}
		vectorOut = vectorSum / vectorArr.Length;
	}

	public void Set(ref Vector3D vector)
	{
		vectorSum = default(Vector3D);
		for (int i = 0; i < vectorArr.Length; i++)
		{
			vectorArr[i] = vector;
			vectorSum += vectorArr[i];
		}
	}
}

public class ProximitySensor
{
	public IMyCameraBlock lidar;

	public float yaw;
	public float pitch;
	public bool failunsafe;
	public bool failasgroup;
	public double distance;
	public double dmsrange;
	public string failunsafeTriggerCommands;
	public string proximityTriggerCommands;

	public bool dmsActive;
	public double dmsPrevDist;

	public ProximitySensor(IMyCameraBlock inputLidar)
	{
		lidar = inputLidar;
	}
}