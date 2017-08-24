
/*
 * D4RK3 54B3R's Skimmer Drive Mode script.
 * This script is a handy script for vehicles that are capable of either driving as a skimmer, or driving as a standard rover.
 * When it is run, it will automatically set the wheel settings appropriate for whichever the current mode is.
 * Run this script with the argument "toggle" to switch between rover mode or skimmer mode.
 * 
 * This is intended with the use of hydrogen skimmers, but can later be expanded to include those powered with atmo-thrusters.
 * 
 *
 *
 * Steps for setting up:
 * 1. Grid needs a programmable block with this on it, along with a Block Group of all of the drive wheels in rover mode.
 *
 * 2. (optional) You can also put on lights on the vehicle to reflect the drive mode of the vehicle.
	RED is for Skimmer mode, YELLOW is for Rover mode.
 *
 * 3. Stick this script on your control toolbar with the argument "toggle"
 * 
 * 4. The global variables below are essentially configuration variables. The names are mostly self-explanatory, and must match the desired settings and block group names.
 * 
 */

 
 
bool driveMode = true; //true for ROVER MODE, false for SKIMMER MODE

const string wheelsGroup = "Drive Wheels"; //This is the name for the wheels group

const string statusLightsGroup = "Mode Lights"; //This is the name for the Lights group. The Lights will reflect the drive status.


const float roverPower = 100;
const float roverFriction = 100;
const float roverStrength = 50;
const float roverDamping = 95;

Color roverColor = new Color(255, 255, 0, 255);


const float skimmerPower = 100;
const float skimmerFriction = 0;
const float skimmerStrength = 75;
const float skimmerDamping = 95;

Color skimmerColor = new Color(255, 0, 0, 255);


/* Leave the stuff below here alone */



public Program() {

    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script. 
    //     
    // The constructor is optional and can be removed if not
    // needed.
	
	Main("");

}


public void Main(string argument) {

    // The main entry point of the script, invoked every time
    // one of the programmable block's Run actions are invoked.
    // 
    // The method itself is required, but the argument above
    // can be removed if not needed.

	if(argument == "toggle"){
		driveMode = !driveMode;
	}
	
	
	//get the wheels list
	IMyBlockGroup wheelsBlockGroup = GridTerminalSystem.GetBlockGroupWithName(wheelsGroup);
	List<IMyMotorSuspension> wheelsList = new List<IMyMotorSuspension>(); 
	wheelsBlockGroup.GetBlocksOfType<IMyMotorSuspension>(wheelsList);
	
	
	/*
	//get the cockpits
	List<IMyCockpit> cockpitsList = new List<IMyCockpit>();
	GridTerminalSystem.GetBlocksOfType<IMyCockpit>(cockpitsList);
	*/
	
	//get the Lights
	IMyBlockGroup lightsBlockGroup = GridTerminalSystem.GetBlockGroupWithName(statusLightsGroup);
	List<IMyInteriorLight> lightsList = new List<IMyInteriorLight>(); 
	lightsBlockGroup?.GetBlocksOfType<IMyInteriorLight>(lightsList);
	
	//get the tanks
	List<IMyGasTank> tanksList = new List<IMyGasTank>(); 
	GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanksList);
	
	
	if(driveMode){ //ROVER MODE
		//UPDATE WHEELS
		Echo("ROVER MODE");
		
		foreach(IMyMotorSuspension wheel in wheelsList){
			wheel.SetValue<float>("Power", roverPower);
			wheel.SetValue<float>("Friction", roverFriction);
			wheel.SetValue<float>("Strength", roverStrength);
			wheel.SetValue<float>("Damping", roverDamping);
			
			wheel.SetValue<bool>("Steering", false);
			wheel.SetValue<bool>("Propulsion", true);
		}
		
		//DISABLE ALL HYDROGEN TANKS
		foreach(IMyGasTank tank in tanksList){
			tank.SetValue<bool>("Stockpile", true);
		}
		
		//UPDATE THE LIGHTS
		foreach(IMyInteriorLight light in lightsList){
			light.SetValue("Color", roverColor);
		}
		
	}else{ //SKIMMER MODE
		//UPDATE WHEELS
		Echo("SKIMMER MODE");
		
		foreach(IMyMotorSuspension wheel in wheelsList){
			wheel.SetValue<float>("Power", skimmerPower);
			wheel.SetValue<float>("Friction", skimmerFriction);
			wheel.SetValue<float>("Strength", skimmerStrength);
			wheel.SetValue<float>("Damping", skimmerDamping);
			
			wheel.SetValue<bool>("Steering", false);
			wheel.SetValue<bool>("Propulsion", false);
		}
		
		//ENABLE ALL HYDROGEN TANKS
		foreach(IMyGasTank tank in tanksList){
			tank.SetValue<bool>("Stockpile", false);
		}
		
		//UPDATE THE LIGHTS
		foreach(IMyInteriorLight light in lightsList){
			light.SetValue("Color", skimmerColor);
		}
		
	}
	
	
}

