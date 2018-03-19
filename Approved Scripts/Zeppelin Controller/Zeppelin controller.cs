/* ZEPPELIN CONTROL PROGRAM 
 * by Solater, edited by D4RK3 54B3R (I added PID control and fixed some other things for TLB)
 *  
 * Quick Start Instructions 
 *  
 * 1. Place a remote control, make sure it is upright and facing in the forward direction. Add to its name, "[rc]"(Without quotations). 
 *  
 * 2a. Place however many balloons as you wish, making sure they are in the same conveyor network. Make sure they are all in a group called "balloon"(Without quotations). 
 * 2b. Place however many hydrogen tanks as you wish, making sure they are in the same conveyor network. Make sure they are all in a group called "ballast"(Without quotations). 
 *  
 * 3. Place your desired amount of oxygen generators. These will be used to fill up the balloons when needed, so, if you are in survival, make sure they are well stocked with ice! Make sure they are on the same conveyor network as the balloons. Make sure they are all in a group called "oxyGen"(Without quotations) 
 *  
 * 4. Place a few hydrogen thrusters facing upward. These will be used to dispose of excess hydrogen in the balloons. Put them in a group called "exhaust"(without quotations). 
 *  
 * 5. Place down a programming block. Open its terminal, click edit and import the script. Now, in the programming block's terminal you will see a text box going by the name of "arguments". This is where you will input all of your configuration. Here the most important arguments are "sea" or "ter". The "sea" argument will allow you to set your altitude based on the sea level. However, the "ter" argument will allow you to set your altitude based on your altitude relative to the ground, this is the altitude that you see on your HUD. So, for example, if you would like to go to an altitude of 3km relative to the ground, you would enter into the arguments section "ter 3"(Without quotations). 
 *  
 * IMPORTANT: The script defaults to support the weaker version of the zeppelin mod. However, if you would like to use the stronger version, you must input the argument "bforce 6000"(Without quotations) in addition to your other arguments. For example, if you wanted to achieve an altitude of 3km relative to the terrain below you, and were using the stronger version of the zeppelin mod, you would use the argument "ter 3;bforce 6000"(Without quotations). 
 *  
 * 6. Place down a timer. Now go into the terminal and edit its actions. Add your programming block, with the action "Run with default argument", as well as your timer, with the action "Trigger now". 
 * (OPTIONAL) I strongly advise you to obtain a gravity alignment script. This script will work without one running, but your experience flying will be worse. To add one of these onto your craft, consult the instruction for the gravity alignment script of your choosing. 
 *  
 * 7. To start the script, go to the Timer's terminal and click "Trigger now". NOTE: It is also possible to run the script every 1 second or even a longer time interval, however its accuracy and safety in flight will decrease. You may wish to consider this, as this script is a considerable burden on the host machine. 
 *  
 * 8. Happy flying! Keep in mind that once you reach the correct filled ratio(as indicated on the left panel in the programming block's terminal), you do not need to continue running the script any longer, and may deactivate it and you will conserve your altitude(unless you change your ship's mass, as this affects the filled ratio needed to achieve a certain altitude) 
 *
 *
 * :SCRIPT COMMANDS:
 * sea <number> 		Set target altitude to <number> kilometers above sea level
 * add <number>			Add <number> to current target altitude
 * reset				Set current altitude to target altitude and reset PID state
 * ter <number>			Set target altitude to <number> kilometers above current ground level
 */ 

//Constant Values 
//static double BALLOON_NEWTONS = 203039.49653; //Amount of Newtons of force exerted by each balloon 

//static double BALLOON_NEWTONS = 4761.90476; //Amount of Newtons of force exerted by each balloon 
static double BALLOON_NEWTONS = 372032.884096; //Amount of Newtons of force exerted by each balloon 
static string BALLOON_NAME = "balloon"; //Balloons' names must be in this group 
static double G = 9.81; //Acceleration imparted by gravity on the earth 
static string RC_NAME = "[rc]"; //Remote control's name must contain this 
static string OXYGEN_NAME = "hydroFarm"; //Oxygen generators must be in this group 
static string THRUST_NAME = "exhaust"; //Thrusters used as exhaust must be in this group 
static string BALLAST_NAME = "ballast";//Ballast tanks used to absorb hydrogen
static string LCD_NAME = "[lcd]"; //LCD used to display information must contain this in its name 
static double ERROR_MARGIN = 0.0015; //Amount of error program will tolerate in terms of filled ratio 

static double lcdUpdateDelay = 1000;
static double lcdUpdateTimer = 0;

double P = 1;
double I = 0.5;
double D = -0.025;
double I_decay = 0.8;

//Variables 
double desiredAltitude = 3.5; 
double currentAltitude = 0;

double currVertVel; 
double lastAltitude; 

int secondsElapsed; 
int msElapsed;

double gravity = G; 
double balloonForce = BALLOON_NEWTONS; 

string planet = "earth"; 

List<IMyGasTank> balloons = new List<IMyGasTank>(); 
List<IMyGasTank> ballasts = new List<IMyGasTank>(); 
IMyRemoteControl rc; 
IMyTextPanel lcd; 
List<IMyOxygenFarm> oxygenGenerators = new List<IMyOxygenFarm>(); 
List<IMyThrust> thrusters = new List<IMyThrust>(); 

bool init = false; 


double PID_sum;
double lastError;
 

public void Main(string argument) 
{ 
	// The main entry point of the script, invoked every time 
	// one of the programmable block's Run actions are invoked. 
	//  
	// The method itself is required, but the argument above 
	// can be removed if not needed. 

	secondsElapsed = Runtime.TimeSinceLastRun.Seconds; 
	msElapsed = Runtime.TimeSinceLastRun.Milliseconds; 
	String printout=""; 
	 

	if (!init) 
	{ 
		findComponents(); 
		Echo("INITIALIZED");
		Runtime.UpdateFrequency = UpdateFrequency.Update10; 
		init = true; 
	} 

	
	
	//
	rc.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out currentAltitude); 
	currentAltitude/=1000;
	findVertVel(currentAltitude); 


	//Argument parsing
	if(!argument.Equals("") && argument != null){
		string[] argumentSplit = argument.Split(';'); 

		for (int i = 0; i < argumentSplit.Length; i++) 
		{ 
			string[] argumentFields = argumentSplit[i].Split(' '); 
			String value; 
			float value_float; 

			if (argumentFields.Length == 2) 
			{ 
				value = argumentFields[1]; 
			} 
			else 
			{ 
				value = null; 
			} 

			switch (argumentFields[0].ToLower()) 
			{ 
				case "reset":
					PID_sum = 0;
					lastError = 0;
					desiredAltitude = currentAltitude;
					break;
				case "add": //altitude addition
					if (!float.TryParse(value, out value_float)) 
					{ 
						Echo("Unable to parse altitude argument"); 
						return; 
					} 
					desiredAltitude += value_float; 
					break;
				case "sea": //Altitude based on sea level 
					if (!float.TryParse(value, out value_float)) 
					{ 
						Echo("Unable to parse altitude argument"); 
						return; 
					} 
					desiredAltitude = value_float; 
					break; 
				case "ter": //Altitude using terrain as reference 
					if (!float.TryParse(value, out value_float)) 
					{ 
						Echo("Unable to parse altitudeterrain argument"); 
						return; 
					} 
					double surfaceAltitude = 0; 
					rc.TryGetPlanetElevation(MyPlanetElevation.Surface, out surfaceAltitude); 
					double difference = (currentAltitude - surfaceAltitude) / 1000; 
					desiredAltitude = value_float + difference; 
					break; 
				case "planet": //planet 
					switch (value) 
					{ 
						case "earth": 
							planet = "earth"; 
							gravity = G * 1; 
							break; 
						case "mars": 
							planet = "mars"; 
							gravity = G * 0.9; 
							break; 
						case "alien": 
							planet = "alien"; 
							gravity = G * 1.1; 
							break; 
					} 
					break; 
				case "bforce": //balloon force 
					if (!float.TryParse(value, out value_float)) 
					{ 
						Echo("Unable to parse balloonforce argument"); 
						return; 
					} 
					balloonForce = value_float * 1000; 
					break; 
			} 

		} 
	}

	printout += "Planet Mode: " + planet+"\n"; 

	bool disable = false;
	if((double)rc.CalculateShipMass().PhysicalMass <= (double)rc.CalculateShipMass().BaseMass) disable = true;
	
	double targetRatio = 0;

	double error = desiredAltitude - currentAltitude;
	double currentRatio = getTotalFilledRatio(); 
	double deltaError = currVertVel;//(error - lastError) * msElapsed / 1000.0;
	
	if(!disable){			
		PID_sum += (error + lastError) * (msElapsed) / 2000.0;
		PID_sum *= I_decay;
	}
	
	targetRatio = P * error + I * PID_sum + D * deltaError + getNeededFilledRatio(desiredAltitude); 	
	
	double deviation = Math.Abs(targetRatio - currentRatio); 
	
	if (currentRatio < targetRatio && deviation > ERROR_MARGIN && !disable) 
	{ 
		toggleThrust(false); 
		toggleOxygen(false); 
		toggleBalloon(true);
		toggleBallast(false);
		
		double avgTankFill = 0;
		foreach(IMyGasTank tank in ballasts){
			avgTankFill += tank.FilledRatio;
		}
		avgTankFill /= ballasts.Count();
		if(avgTankFill <= 0.1){
			toggleOxygen(true);
		}
		
		printout += "Increasing filled ratio\n"; 
	} 
	else if (currentRatio > targetRatio && deviation > ERROR_MARGIN && !disable) 
	{ 
		toggleThrust(false); 
		toggleOxygen(false); 
		toggleBallast(true);
		toggleBalloon(false);
		
		double avgTankFill = 0;
		foreach(IMyGasTank tank in ballasts){
			avgTankFill += tank.FilledRatio;
		}
		avgTankFill /= ballasts.Count();
		if(avgTankFill > 0.999){
			toggleThrust(true);
		}
		
		printout += "Decreasing filled ratio\n"; 
	} 
	else 
	{ 
		toggleThrust(false); 
		toggleOxygen(false); 
		toggleBallast(false);
		toggleBalloon(false);
		
		printout += "Maintaining filled ratio\n"; 
	} 

	
	
	
	
	//print output
	lastError = error;
	lcdUpdateTimer -= msElapsed;
	
	if(lcdUpdateTimer <= 0){
		lcdUpdateTimer = lcdUpdateDelay;
		if (targetRatio > 1) 
		{ 
			printout += "Desired altitude impossible to reach, moving to maximum possible\n"; 
		} 
		if(disable) printout += "Zeppelin is parked... " + "\n"; 
		printout += "Current filled ratio: " + Math.Round(currentRatio * 100, 3) + "%\n"; 
		printout += "Target filled ratio: " + Math.Round(targetRatio * 100, 3) + "%\n"; 
		printout += "Current Altitude: " + Math.Round(currentAltitude, 3) + " km\n"; 
		printout += "Desired altitude: " + Math.Round(desiredAltitude, 3) + " km\n"; 
		//bouyancy altitude display don't work :(
		//double bouyancyAltitude = (balloons.Count * balloonForce) / ((double)rc.CalculateShipMass().PhysicalMass * gravity);
		//bouyancyAltitude = estimateBouyancyAltitude(1 / bouyancyAltitude);
		//printout += "Bouyant Altitude: ~" + Math.Round(bouyancyAltitude, 3) + " km\n"; 
		printout += "Current vertical velocity: " + Math.Round(currVertVel, 2) + " m/s\n"; 
		printout += "Atmo Density: " + Math.Round(getAtmosphericDensity(currentAltitude), 2) + " \n"; 

		printout+="\n";
		
		Echo(printout); 

		if (lcd != null) 
		{ 
			lcd.WritePublicText(printout); 
		} 
	}

} 

void findComponents() 
{ 


	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(); 
	GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(blocks); 


	foreach (IMyRemoteControl block in blocks) 
	{ 
		if (block.CustomName.Contains(RC_NAME)) 
		{ 
			rc = block as IMyRemoteControl; 
		} 
	} 
	
	GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(blocks); 

	foreach (IMyTextPanel block in blocks) 
	{ 
		if (block.CustomName.Contains(LCD_NAME)) 
		{ 
			lcd = block as IMyTextPanel; 
			lcd.WritePublicText("Waiting for commands");
		} 
	} 
	
	GridTerminalSystem.GetBlockGroupWithName(BALLOON_NAME)?.GetBlocksOfType<IMyGasTank>(balloons); 
	GridTerminalSystem.GetBlockGroupWithName(BALLAST_NAME)?.GetBlocksOfType<IMyGasTank>(ballasts); 
	GridTerminalSystem.GetBlockGroupWithName(THRUST_NAME)?.GetBlocksOfType<IMyThrust>(thrusters); 
	GridTerminalSystem.GetBlockGroupWithName(OXYGEN_NAME)?.GetBlocksOfType<IMyOxygenFarm>(oxygenGenerators); 


	Echo("Balloons - " + balloons.Count); 

	foreach (IMyGasTank balloon in balloons) 
	{ 
		Echo(balloon.CustomName); 
	} 
	
	Echo("Ballasts - " + ballasts.Count); 

	foreach (IMyGasTank tank in ballasts) 
	{ 
		Echo(tank.CustomName); 
	} 

	Echo("Remote Control - " + rc.CustomName); 

	Echo("Oxygen Generators - " + oxygenGenerators.Count); 
	
	
	foreach (IMyOxygenFarm oxygen in oxygenGenerators) 
	{ 
		Echo(oxygen.CustomName); 
	}
	

	Echo("Thrusters - " + thrusters.Count); 

	foreach (IMyThrust thrust in thrusters) 
	{ 
		Echo(thrust.CustomName); 
	} 

} 

double estimateBouyancyAltitude(double density)
{ 
	double alt = 0; 
	if (planet.Equals("earth") || planet.Equals("mars")) 
	{ 
		if(density > 1) density = 1;
		alt = (density - 0.999714809) / (-0.0712151286);
	} 
	else if (planet.Equals("alien")) 
	{ 
		if(density > 1.2) density = 1.2;
		alt = (density - 1.20032669) / (-0.0854040886);
	} 
	return alt; 
} 

double getAtmosphericDensity(double altitudeKM) 
{ 
	double eff = 0; 
	if (planet.Equals("earth") || planet.Equals("mars")) 
	{ 
		eff = -0.0712151286 * altitudeKM + 0.999714809; 
		if (eff > 1) 
		{ 
			eff = 1; 
		} 
	} 
	else if (planet.Equals("alien")) 
	{ 
		eff = -0.0854040886 * altitudeKM + 1.20032669; 
		if (eff > 1.2) 
		{ 
			eff = 1.2; 
		} 
	} 
	return eff; 
} 

double getNeededFilledRatio(double desiredAltitude) 
{ 
	double ratio = ((double)rc.CalculateShipMass().PhysicalMass * gravity) / (balloons.Count * balloonForce * getAtmosphericDensity(desiredAltitude)); 
	return ratio; 
} 

double getTotalFilledRatio() 
{ 
	double total = 0; 

	foreach (IMyGasTank balloon in balloons) 
	{ 
		total += balloon.FilledRatio; 
	} 

	return total / balloons.Count(); 
} 

void toggleThrust(Boolean on) 
{ 
	foreach (IMyThrust thrust in thrusters) 
	{ 
		thrust.SetValueFloat("Override", 100); 
		thrust.Enabled = on; 
	} 
} 

void toggleOxygen(Boolean on) 
{ 
	foreach (IMyOxygenFarm oxygen in oxygenGenerators) 
	{ 
		if(on){
			oxygen.ApplyAction("OnOff_On");
		}else{
			oxygen.ApplyAction("OnOff_Off");
		}
	} 
} 

void toggleBallast(Boolean on)
{
	foreach(IMyGasTank tank in ballasts){
		tank.Stockpile = on;
	}
}

void toggleBalloon(Boolean on){
	foreach(IMyGasTank balloon in balloons){
		balloon.Stockpile = on;
	}
}

void findVertVel(double currentAltitudeM) 
{ 
	Vector3D gravVec = rc.GetNaturalGravity();
	gravVec.Normalize();
	
	currVertVel = -rc.GetShipVelocities().LinearVelocity.Dot(gravVec);
	lastAltitude = currentAltitudeM; 
}