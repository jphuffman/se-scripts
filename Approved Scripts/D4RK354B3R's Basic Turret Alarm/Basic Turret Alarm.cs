
/*
 * D4RK3's basic Turret Alarm script
 * 
 * This shit checks all turrets on the grid once every 1.6 seconds.
 * If any of the turrets are tracking a target, then the alarm will be set off.
 * The alarm is literally just a beacon that flickers on.
 * 
 * If the turrets haven't been active for 30 seconds, then the alarm deactivates.
 */

string beaconName = "BASE UNDER ATTACK"; //This is the name of the alarm beacon. caps sensitive
string timerName = "";

long flickerTime = 30; //This is how long the beacon flickers after an alarm has been set off. set to 0 or negative to disable.

//dont touch shit below this

//making scriptTime a long allows me to keep 1-second accuracy to 24,855 consecutive days of script running.
//No SE server will ever run that long without restart.
double deltaTime = 0;
long scriptTime = 0; //# of seconds that the script has been running.
double subSecond = 0;

long alertTime = 0; //the time when target is first found
long cooloffTime = 0; //the time when no targets exist anymore
bool alerted = false;
bool lastAlert = false;
long alertCooldown = 30; //the time it takes for alarm to deactivate

IMyBeacon alertBeacon = null;
IMyTimerBlock alertTimer = null;
Dictionary<long, double> damageTable = new Dictionary<long, double>();



public Program()
{
	// The constructor, called only once every session and
	// always before any other method is called. Use it to
	// initialize your script.

	Runtime.UpdateFrequency = UpdateFrequency.Update10; //this script runs once every 0.166 seconds

	findAlertBeacon();
}

public void Main(string args)
{
	// The main entry point of the script, invoked every time
	// one of the programmable block's Run actions are invoked.

	// The method itself is required, but the argument above
	// can be removed if not needed.
	
	if (Runtime.TimeSinceLastRun.TotalSeconds != 0)
	{
		deltaTime = Runtime.TimeSinceLastRun.TotalSeconds;
		
		subSecond += deltaTime;

		if ((int)subSecond > 0) {
			scriptTime += (int)(subSecond);
			subSecond -= (int)(subSecond);
		}
		else
		{
			return; //Make sure that it only runs once a second (approximately)
		}
	}
	else
	{
		return; //wut
	}

	Echo("Running " + scriptTime);

	if (alertBeacon == null)
	{
		Echo("NULL ALERT BEACON"); //look for a new beacon yo
		findAlertBeacon();
	}

	if(alertTimer == null)
	{
		findTimerBlock();
	}

	List<IMyLargeConveyorTurretBase> guns = new List<IMyLargeConveyorTurretBase>();
	GridTerminalSystem.GetBlocksOfType<IMyLargeConveyorTurretBase>(guns);

	bool newAlert = false;

	foreach (IMyLargeConveyorTurretBase gun in guns)
	{
		if (gun == null) continue;
		long id = gun.EntityId;

		//get the new health value
		IMySlimBlock slim = gun.CubeGrid.GetCubeBlock(gun.Position);
		double damage = slim.CurrentDamage;

		double oldDamage = damage; //now update oldHealth from the dictionary
		if (damageTable.ContainsKey(id))
		{
			damageTable.TryGetValue(id, out oldDamage);

			if (oldDamage < damage) newAlert = true;
		}
		//update the dictionary with new health values
		
		damageTable[id] = damage;

	}

	foreach (IMyLargeConveyorTurretBase gun in guns) {
		if (gun == null) continue;
		if (newAlert) break;
		//check to make sure that the target is not empty
		MyDetectedEntityInfo target = gun.GetTargetedEntity();
		if (target.IsEmpty()) continue;

		if (!target.Relationship.Equals(MyRelationsBetweenPlayerAndBlock.Enemies)) continue;

		newAlert = true;
		break;
	}

	if (newAlert)
	{
		if (!alerted) //alarm just activated.
		{
			alertTime = scriptTime;
			cooloffTime = scriptTime;

			if(alertTimer != null)
			{
				alertTimer.StartCountdown();
				alertTimer.ApplyAction("OnOff_On");
			}
		}
		alerted = true;

		alertBeacon?.ApplyAction("OnOff_On");

		Echo("ALERT");
	}
	else
	{
		Echo("No Targets " + (cooloffTime + alertCooldown));
		if (lastAlert) //first second where there's no new targets
			cooloffTime = scriptTime; //record cooloffTime

		if(alerted && scriptTime > cooloffTime + alertCooldown){
			alertBeacon?.ApplyAction("OnOff_Off");
			alerted = false;
			cooloffTime = 0;
			alertTime = 0;
		} else if (alerted)
		{
			alertBeacon?.ApplyAction("OnOff_On");
		}
	}

	//if alarm is on and I should flicker
	if (alerted && scriptTime < alertTime + flickerTime)
	{
		Echo("Flicker");
		//flicker the beacon
		if ((scriptTime) % 2 == 0)
			alertBeacon?.ApplyAction("OnOff_On");
		else
			alertBeacon?.ApplyAction("OnOff_Off");
	}

	lastAlert = newAlert;
}

void findAlertBeacon()
{
	List<IMyBeacon> beacons = new List<IMyBeacon>();
	GridTerminalSystem.GetBlocksOfType<IMyBeacon>(beacons);
	foreach (IMyBeacon beacon in beacons)
	{
		if (beacon.CustomName.Contains(beaconName))
		{
			alertBeacon = beacon;
			alertBeacon.ApplyAction("OnOff_Off");
			alertBeacon.Radius = 50000;
			alerted = false;
			return;
		}
	}
}

void findTimerBlock()
{
	if (timerName.Length == 0) return;

	List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(timers);
	foreach (IMyTimerBlock timer in timers)
	{
		if (timer.CustomName.Contains(timerName))
		{
			alertTimer = timer;
			return;
		}
	}
}
