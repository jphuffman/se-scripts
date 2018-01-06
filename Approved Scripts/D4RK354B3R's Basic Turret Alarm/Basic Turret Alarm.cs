
/*
 * D4RK3's basic Turret Alarm script
 * 
 * This shit checks all turrets on the grid once every 1.6 seconds.
 * If any of the turrets are tracking a target, then the alarm will be set off.
 * The alarm is literally just a beacon that flickers on.
 * 
 * If the turrets haven't been active for 30 seconds, then the alarm deactivates.
 *
 * SETUP:
 * 1. Stick this script on a programmable block
 * 2. Put turrets on the same grid. no groups or naming needed.
 * 3. Stick a beacon on the same grid. this beacon's name must reflect the beaconName variable.
 */

string beaconName = "BASE UNDER ATTACK"; //This is the name of the alarm beacon. caps sensitive

long flickerTime = 10; //This is how long the beacon flickers after an alarm has been set off. set to 0 or negative to disable.

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

	Echo("Running " + scriptTime);
	
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

	if (alertBeacon == null)
	{
		Echo("NULL ALERT BEACON"); //look for a new beacon yo
		findAlertBeacon();
	}

	List<IMyLargeConveyorTurretBase> guns = new List<IMyLargeConveyorTurretBase>();
	GridTerminalSystem.GetBlocksOfType<IMyLargeConveyorTurretBase>(guns);

	bool newAlert = false;

	foreach(IMyLargeConveyorTurretBase gun in guns) {
		if (gun == null) continue;
		MyDetectedEntityInfo target = gun.GetTargetedEntity();
		if (target.IsEmpty()) continue;

		newAlert = true;
		break;
	}

	if (newAlert)
	{
		if (!alerted) //alarm just activated.
		{
			alertTime = scriptTime;
			cooloffTime = scriptTime;
		}
		alerted = true;

		alertBeacon?.ApplyAction("OnOff_On");
	}
	else
	{
		if(lastAlert) //first second where there's no new targets
			cooloffTime = scriptTime; //record cooloffTime

		if(alerted && scriptTime > cooloffTime + alertCooldown){
			alertBeacon?.ApplyAction("OnOff_Off");
			alerted = false;
		}

	}

	//if alarm is on and I should flicker
	if (alerted && scriptTime < alertTime + flickerTime)
	{
		//flicker the beacon
		if (scriptTime % 2 == 0)
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
			break;
		}
	}
}
