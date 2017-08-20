// Solar Panel Alignment Script by Isy 
// ============================ 
// Version: 3.4.1 
// Date: 2017-08-16 
   
// ======================================================================================= 
//                                                                            --- Configuration --- 
// ======================================================================================= 
   
// --- Essential Configuration --- 
// ======================= 
 
// Name of the group with all the solar related rotors 
string rotorGroupName = "Solar Rotors"; 
 
// Timer setup: 
// Delay: 1 second 
// First action: Programmable block - "Run" 
// Second action: Timer block - "Start" 
   
   
// --- Optional Configuration --- 
// ====================== 
        
// Rotate the panels towards the sunrise during the night? (Possible values: true | false, default: true) 
// The angle is figured out automatically based on the first lock of the day. 
// If you want to set the angles yourself, set manualAngle to true and adjust the angles to your likings. 
bool rotateToSunrise = true; 
bool manualAngle = false; 
int manualAngleVertical= 0; 
int manualAngleHorizontal = 0; 
   
// List of LCD Panels to display the current operations and efficiency (single Names or group names). 
// Example: string[] regularLcds = { "LCD Solar Alignment", "LCD 2", "LCD Solar Output" } 
string[] regularLcds = { "LCD Solar Alignment" }; 
bool showBatteryStats = true; 
bool showOxygenStats = true; 
bool showLocationTime = true; 
 
// List of corner LCD Panels to display basic output information (single Names or group names). 
// Example: string[] cornerLcds = { "Corner LCD 1", "Corner LCD 2", "LCD Solar Output" } 
// Optional: Put keywords in the LCD's custom data for different stats: time, battery, oxygen 
string[] cornerLcds = {}; 
 
// Enable base light management? (Possible values: true | false, default: false) 
// Lights will be turned on/off based on daytime. 
bool baseLightManagement = false; 
 
// Define the times when your lights should go on or off.  
int lightOffHour = 8; 
int lightOnHour = 18; 
 
// To only toggle specific lights, declare groups for them. 
// Example: string[] baseLightGroups = { "Interior Lights", "Spotlights", "Hangar Lights" } 
string[] baseLightGroups = {}; 
 
// Trigger external timer blocks at specific events? (action "Start" will be applied which takes the delay into account) 
// Events can be: "sunrise", "sunset", a time like "15:00" or a number for every X seconds 
// Every event needs a timer block name in the exact same order as the events. 
// Calling the same timer block with multiple events requires it's name multiple times in the timers list! 
// Example: 
// string[] events = { "sunrise", "sunset", "15:00", "30" }; 
// string[] timers = { "Timer 1", "Timer 1", "Timer 2", "Timer 3" }; 
// This will trigger "Timer 1" at sunrise and sunset, "Timer 2" at 15:00 and "Timer 3" every 30 seconds. 
bool triggerTimerBlock = false; 
string[] events = {}; 
string[] timers = {}; 
 
// ======================================================================================= 
//                                                                      --- End of Configuration --- 
//                                                        Don't change anything beyond this point! 
// ======================================================================================= 
       
   
// Output variables 
double maxOutputAP = 0; 
double maxOutputAPLast = 0; 
double maxDetectedOutputAP = 0; 
double currentOutputAP = 0; 
double maxUsedOutputAP = 0; 
int solarPanelsCount = 0; 
     
// Lists 
List<IMyMotorStator> rotors = new List<IMyMotorStator>(); 
List<IMyMotorStator> Vrotors = new List<IMyMotorStator>(); 
List<IMyMotorStator> Hrotors = new List<IMyMotorStator>(); 
 
// Rotor variables 
const float rotorSpeed = 0.2f; 
const float rotorSpeedFast = 1.0f; 
bool rotationDone = false; 
string defaultCustomData = "0\n0\n0\n0\n1\n0\n0\n1\n0\n0\n0"; 
 
// Battery variables 
double batteriesCurrentInput = 0; 
double batteriesMaxInput = 0; 
double batteriesCurrentOutput = 0; 
double batteriesMaxOutput = 0; 
double batteriesPower = 0; 
double batteriesMaxPower = 0; 
int batteriesCount = 0; 
 
// Oxygen farm and tank variables 
double oxygenFarmEfficiency = 0; 
int oxygenFarmCount = 0; 
double oxygenTankFillLevel = 0; 
int oxygenTankCount = 0; 
 
// String variables for showing the information 
string maxOutputAPString = "0 kW"; 
string maxDetectedOutputAPString = "0 kW"; 
string currentOutputAPString = "0 kW"; 
string maxUsedOutputString = "0 kW"; 
string informationString = ""; 
 
// Variables for time measuring 
int dayTimer = 0; 
int safetyTimer = 120; 
int sunSet = 0; 
int midNight = 0; 
int dayLength = 0; 
double nightDetectionMultiplier = 0.50; 
 
// LCD Scrolling 
int scrollDirection = 1; 
int scrollWait = 3; 
int lineStart = 3; 
 
// Error handling 
string error = ""; 
string warning = ""; 
 
// First run variable 
bool firstRun = true; 
 
// Action variable for storing parameters 
string action = ""; 
int actionTimer = 3; 
 
// Load variables out the programmable block's custom data field 
public Program() { 
    if (Me.CustomData.Length > 0) { 
        var parts = Me.CustomData.Split('=',';'); 
        if (parts.Length >= 9) { 
            dayTimer = int.Parse(parts[1]); 
            safetyTimer = int.Parse(parts[3]); 
            dayLength = int.Parse(parts[5]); 
            sunSet = int.Parse(parts[7]); 
            maxOutputAPLast = double.Parse(parts[9]); 
            Echo ("Restored location time calculation!"); 
        } 
    } 
} 
   
void Main(string parameter) { 
    // On the first run, initialize all rotors and stop them, don't stop them in further runs 
    if (firstRun) { 
        initialize(); 
        stopAll(); 
         
        // Reset max output for clean statistics 
        foreach (var rotor in rotors) { 
            writeCustomData(rotor, "outputMax", 0); 
        } 
         
        firstRun = false; 
    } else { 
        initialize(); 
    } 
     
    // Output String 
    informationString = "Isy's Solar Alignment Script\n======================\n\n"; 
     
    // Add warning message for minor errors 
    if (warning.Length != 0 && error.Length == 0) { 
        informationString += "Warning!\n"; 
        informationString += warning; 
        informationString += "Check your settings! Continuing...\n\n"; 
        warning = ""; 
    } 
         
    // Get the output of the solars 
    getOutput(); 
     
    // Time Calculation 
    if (showLocationTime) timeCalculation(); 
     
    // Set showLocationTime to true if baseLightManagement or triggerTimerBlock is true 
    if (baseLightManagement || triggerTimerBlock) showLocationTime = true; 
     
    // Switch the lights if base light management is activated 
    if (baseLightManagement) lightManagement(); 
     
    // Trigger a timer block if triggerTimerBlock is true 
    if (triggerTimerBlock) triggerExternalTimerBlock(); 
     
    // Get parameter 
    if (parameter != "") action = parameter; 
     
    // If any error occurs, show it and stop every operation 
    if (error.Length != 0) { 
        stopAll(); 
         
        informationString += "Error!\n"; 
        informationString += error; 
        informationString += "Check your settings! Script stopped!\n"; 
         
    // Pause the alignment when set via argument 
    } else if (action == "pause") { 
        stopAll(); 
             
        informationString += "Automatic alignment paused.\n"; 
        informationString += "Run with 'resume' to continue..\n"; 
         
    // Reset the time calculation when set via argument 
    } else if (action == "reset") { 
        dayTimer = 0; 
        sunSet = 0; 
        midNight = 0; 
        dayLength = 0; 
     
        informationString += "Calculated time resetted.\n"; 
        informationString += "Continuing in " + actionTimer + " seconds.\n"; 
         
        actionTimer -= 1; 
        if (actionTimer == 0) { 
            action = ""; 
            actionTimer = 3; 
        } 
        
    // Rotate to a specific angle when set via argument 
    } else if (action.IndexOf("rotate") >= 0) { 
        String[] angles = action.Split(' '); 
        int horizontalAngle = 90; 
        int verticalAngle = 90; 
     
        if (angles.Length == 2) { 
            if (!rotationDone) { 
                informationString += "Rotating to user defined values...\n"; 
            } else { 
                informationString += "Rotation done.\n"; 
                informationString += "Automatic alignment paused.\n"; 
            } 
            Int32.TryParse(angles[1], out horizontalAngle); 
            rotateAll(horizontalAngle); 
        } else if (angles.Length == 3) { 
            if (!rotationDone) { 
                informationString += "Rotating to user defined values...\n"; 
            } else { 
                informationString += "Rotation done.\n"; 
                informationString += "Automatic alignment paused.\n"; 
            } 
            Int32.TryParse(angles[1], out horizontalAngle); 
            Int32.TryParse(angles[2], out verticalAngle); 
            rotateAll(horizontalAngle, verticalAngle); 
        } else { 
            stopAll(); 
            informationString += "Wrong format!\n\n"; 
            informationString += "Should be:\nrotate horizontalAngle verticalAngle\n\n"; 
            informationString += "Example:\nrotate 90 90\n"; 
        } 
         
    // Execute the main rotation logic 
    } else { 
        rotationLogic(); 
    } 
     
    // Create the information string that is shown to the user 
    createInformation(); 
     
    // Write the information to various channels 
    Echo(informationString); 
    writeLCD(); 
    writeCornerLCD(); 
             
    // Update variables for the next run 
    foreach (var rotor in rotors) { 
        double outputLast = readCustomData(rotor, "output"); 
        writeCustomData(rotor, "outputLast", outputLast); 
    } 
    maxOutputAPLast = maxOutputAP; 
} 
 
 
// ======================================================================================= 
// Method for initializing and sorting the rotors 
// ======================================================================================= 
 
void initialize() { 
    // Get the rotor group 
    var rotorGroup = GridTerminalSystem.GetBlockGroupWithName(rotorGroupName); 
      
    // If present, copy rotors into rotors list, else throw message 
    if (rotorGroup != null) { 
        rotorGroup.GetBlocksOfType<IMyMotorStator>(rotors); 
        error = ""; 
    } else { 
        error = "Rotor group not found:\n'" + rotorGroupName + "'\n\n"; 
    } 
     
    var grids = new List<IMyCubeGrid>(); 
     
    // Get unique grids and prepare the rotors 
    foreach (var rotor in rotors) { 
        if (!grids.Exists(grid => grid == rotor.CubeGrid)) { 
            grids.Add(rotor.CubeGrid); 
        } 
         
        // Set basic stats for every rotor 
        rotor.SetValue("Weld speed", 20f); 
        rotor.SetValue("Torque", 33600000f); 
        rotor.SetValue("BrakingTorque", 33600000f); 
    } 
     
    // Find vertical and horizontal rotors and add them to their respective list 
    Vrotors.Clear(); 
    Hrotors.Clear(); 
    foreach (var rotor in rotors) { 
        if (grids.Exists(grid => grid == rotor.TopGrid)) { 
            Vrotors.Add(rotor); 
        } else { 
            Hrotors.Add(rotor); 
        } 
    } 
     
    // Check, if a U-Shape is used and rebuild the list with only one of the connected rotors 
    List<IMyMotorStator> HrotorsTemp = new List<IMyMotorStator>(); 
    HrotorsTemp.AddRange(Hrotors); 
    Hrotors.Clear(); 
    bool addRotor; 
     
    foreach (var rotorTemp in HrotorsTemp) { 
        addRotor = true; 
         
        foreach (var rotor in Hrotors) { 
            if (rotor.TopGrid == rotorTemp.TopGrid) { 
                rotorTemp.SetValue("Velocity", 0f); 
                rotorTemp.SetValueBool("Force weld", false); 
                rotorTemp.SetValue("Torque", 0f); 
                rotorTemp.SetValue("BrakingTorque", 0f); 
                addRotor = false; 
                break; 
            } 
        } 
         
        if (addRotor) Hrotors.Add(rotorTemp); 
    } 
} 
 
 
// ======================================================================================= 
// Method for reading a rotor's custom data field, returns double 
// ======================================================================================= 
 
double readCustomData(IMyMotorStator rotor, string field) { 
    var customData = rotor.CustomData.Split('\n'); 
     
    // Create new blank customData if a too short one is found 
    if (customData.Length < 11) { 
        clearCustomData(rotor); 
        customData = rotor.CustomData.Split('\n'); 
    } 
     
    switch (field) { 
        case "output": 
            return Convert.ToDouble(customData[0]); 
        case "outputLast": 
            return Convert.ToDouble(customData[1]); 
        case "outputLocked": 
            return Convert.ToDouble(customData[2]); 
        case "outputMax": 
            return Convert.ToDouble(customData[3]); 
        case "direction": 
            return Convert.ToDouble(customData[4]); 
        case "directionChanged": 
            return Convert.ToDouble(customData[5]); 
        case "directionTimer": 
            return Convert.ToDouble(customData[6]); 
        case "allowRotation": 
            return Convert.ToDouble(customData[7]); 
        case "timeSinceRotation": 
            return Convert.ToDouble(customData[8]); 
        case "firstLockOfDay": 
            return Convert.ToDouble(customData[9]); 
        case "sunriseAngle": 
            return Convert.ToDouble(customData[10]); 
        default: 
            return 0; 
    } 
} 
 
 
// ======================================================================================= 
// Method for writing a rotor's custom data field 
// ======================================================================================= 
 
void writeCustomData(IMyMotorStator rotor, string field, double value = 0) { 
    var customData = rotor.CustomData.Split('\n'); 
     
    // Create new blank customData if a too short one is found 
    if (customData.Length < 11) { 
        clearCustomData(rotor); 
        customData = rotor.CustomData.Split('\n'); 
    } 
     
    switch (field) { 
        case "output": 
            customData[0] = value.ToString(); 
            break; 
        case "outputLast": 
            customData[1] = value.ToString(); 
            break; 
        case "outputLocked": 
            customData[2] = value.ToString(); 
            break; 
        case "outputMax": 
            customData[3] = value.ToString(); 
            break; 
        case "direction": 
            customData[4] = value.ToString(); 
            break; 
        case "directionChanged": 
            customData[5] = value.ToString(); 
            break; 
        case "directionTimer": 
            customData[6] = value.ToString(); 
            break; 
        case "allowRotation": 
            customData[7] = value.ToString(); 
            break; 
        case "timeSinceRotation": 
            customData[8] = value.ToString(); 
            break; 
        case "firstLockOfDay": 
            customData[9] = value.ToString(); 
            break; 
        case "sunriseAngle": 
            customData[10] = value.ToString(); 
            break; 
    } 
     
    string newCustomData = ""; 
    foreach (var data in customData) { 
        newCustomData += data + "\n"; 
    } 
    newCustomData = newCustomData.TrimEnd('\n'); 
    rotor.CustomData = newCustomData; 
} 
 
 
// ======================================================================================= 
// Method for clearing a single rotor's custom data field 
// ======================================================================================= 
 
void clearCustomData(IMyMotorStator rotor) { 
    rotor.CustomData = defaultCustomData; 
} 
 
 
// ======================================================================================= 
// Method for clearing all rotor's custom data fields 
// ======================================================================================= 
 
void clearAllCustomData() { 
    foreach (var rotor in rotors) { 
        rotor.CustomData = defaultCustomData; 
    } 
} 
 
 
// ======================================================================================= 
// Method for reading and converting the current panel output 
// ======================================================================================= 
 
void getOutput() { 
    // Get all Solar Panels 
    var allSolarPanels = new List<IMySolarPanel>(); 
    var rotorSolarPanels = new List<IMySolarPanel>(); 
    GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(allSolarPanels); 
     
    // Get all Oxygen Farms 
    var oxygenFarms = new List<IMyOxygenFarm>(); 
    var rotorOxygenFarms = new List<IMyOxygenFarm>(); 
    GridTerminalSystem.GetBlocksOfType<IMyOxygenFarm>(oxygenFarms); 
     
    maxOutputAP = 0; 
    currentOutputAP = 0; 
     
    // Get the ones that are on our Hrotors 
    // Also add their current output for each rotor and store them in the rotor's custom data field 
    foreach (var Hrotor in Hrotors) { 
        double output = 0; 
         
        // Find all solar panels that are on our rotors 
        bool hasSolars = false; 
        foreach (var solarPanel in allSolarPanels) { 
            if (solarPanel.CubeGrid == Hrotor.TopGrid) { 
                rotorSolarPanels.Add(solarPanel); 
                output += solarPanel.MaxOutput; 
                maxOutputAP += solarPanel.MaxOutput; 
                currentOutputAP += solarPanel.CurrentOutput; 
                hasSolars = true; 
            } 
        } 
         
        // Find all oxygen farms that are on our rotors 
        bool hasOxygenFarms = false; 
        foreach (var oxygenFarm in oxygenFarms) { 
            if (oxygenFarm.CubeGrid == Hrotor.TopGrid) { 
                rotorOxygenFarms.Add(oxygenFarm); 
                output += oxygenFarm.GetOutput(); 
                maxOutputAP += oxygenFarm.GetOutput() / 1000000; 
                hasOxygenFarms = true; 
            } 
        } 
         
        // Print a warning if a rotor has neither a solar panel nor an oxygen farm 
        if (!hasSolars && !hasOxygenFarms) { 
            warning += "'" + Hrotor.CustomName + "' can't align!\nAdd a solar panel or oxygen farm to it!\n\n"; 
        } 
         
        // Write the output in the custom data field 
        writeCustomData(Hrotor, "output", output); 
         
        // If it's higher than the max detected output, write it, too 
        if (output > readCustomData(Hrotor, "outputMax")) { 
            writeCustomData(Hrotor, "outputMax", output); 
        } 
    } 
    solarPanelsCount = rotorSolarPanels.Count; 
    oxygenFarmCount = rotorOxygenFarms.Count; 
     
    // Show warning if no solar panels or oxygen farms were found 
    if (solarPanelsCount == 0 && oxygenFarmCount == 0) { 
        warning += "No solar panels or oxygen farms found!\nHow should I see the sun now?\n\n"; 
    } 
     
    // Read and store the combined output of all Hrotors that are on top of Vrotors 
    foreach (var Vrotor in Vrotors) { 
        double output = 0; 
     
        foreach (var Hrotor in Hrotors) { 
            if (Hrotor.CubeGrid == Vrotor.TopGrid) { 
                output += readCustomData(Hrotor, "output"); 
            } 
        } 
         
        // Write the output in the custom data field 
        writeCustomData(Vrotor, "output", output); 
         
        // If it's higher than the max detected output, write it, too 
        if (output > readCustomData(Vrotor, "outputMax")) { 
            writeCustomData(Vrotor, "outputMax", output); 
        } 
    } 
     
    // Set and format max. detected output 
    if (maxOutputAP > maxDetectedOutputAP) { 
        maxDetectedOutputAP = maxOutputAP; 
        if (maxDetectedOutputAP < 1) { 
            maxDetectedOutputAPString = Math.Round(maxDetectedOutputAP * 1000, 2) + " kW"; 
        } else { 
            maxDetectedOutputAPString = Math.Round(maxDetectedOutputAP, 2) + " MW"; 
        } 
    } 
     
    // Set and format max. used output 
    if (currentOutputAP > maxUsedOutputAP) { 
        maxUsedOutputAP = currentOutputAP; 
        if (maxUsedOutputAP < 1) { 
            maxUsedOutputString = Math.Round(maxUsedOutputAP * 1000, 2) + " kW"; 
        } else { 
            maxUsedOutputString = Math.Round(maxUsedOutputAP, 2) + " MW"; 
        } 
    } 
     
    // Format the output strings 
    maxOutputAPString = Math.Round(maxOutputAP, 2) + " MW"; 
    if (maxOutputAP < 1) maxOutputAPString = Math.Round(maxOutputAP * 1000, 2) + " kW"; 
      
    currentOutputAPString = Math.Round(currentOutputAP, 2) + " MW"; 
    if (currentOutputAP < 1) currentOutputAPString = Math.Round(currentOutputAP * 1000, 2) + " kW"; 
     
    // Find batteries 
    if (showBatteryStats) { 
        var batteries = new List<IMyBatteryBlock>(); 
        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries); 
     
        // Reset variables 
        batteriesCurrentInput = 0; 
        batteriesMaxInput = 0; 
        batteriesCurrentOutput = 0; 
        batteriesMaxOutput = 0; 
        batteriesPower = 0; 
        batteriesMaxPower = 0; 
        batteriesCount = 0; 
         
        // Add their current values 
        foreach (var battery in batteries) { 
            batteriesCurrentInput += battery.CurrentInput; 
            batteriesMaxInput += getBatteryValues(battery, 1); 
            batteriesCurrentOutput += battery.CurrentOutput; 
            batteriesMaxOutput += getBatteryValues(battery, 2); 
            batteriesPower += battery.CurrentStoredPower; 
            batteriesMaxPower += battery.MaxStoredPower; 
            batteriesCount += 1; 
        } 
         
        // Round the values to be nicely readable 
        batteriesCurrentInput = Math.Round(batteriesCurrentInput, 2); 
        batteriesMaxInput = Math.Round(batteriesMaxInput, 2); 
        batteriesCurrentOutput = Math.Round(batteriesCurrentOutput, 2); 
        batteriesMaxOutput = Math.Round(batteriesMaxOutput, 2); 
        batteriesPower = Math.Round(batteriesPower, 2); 
        batteriesMaxPower = Math.Round(batteriesMaxPower, 2); 
         
        // Show warning if no battery was found 
        if (batteriesCount == 0) { 
            warning += "No batteries found!\nDon't you want to store your Power?\n\n"; 
        } 
    } 
     
    // Find oxygen farms and tanks 
    if (showOxygenStats) { 
        var oxygenTanks = new List<IMyGasTank>(); 
        GridTerminalSystem.GetBlocksOfType<IMyGasTank>(oxygenTanks); 
         
        // Reset Variables 
        oxygenFarmEfficiency = 0; 
        oxygenFarmCount = 0; 
        oxygenTankFillLevel = 0; 
        oxygenTankCount = 0; 
         
        foreach (var oxygenFarm in rotorOxygenFarms) { 
            oxygenFarmEfficiency += oxygenFarm.GetOutput(); 
            oxygenFarmCount++; 
        } 
         
        oxygenFarmEfficiency = Math.Round(oxygenFarmEfficiency / oxygenFarmCount * 100, 2); 
         
        foreach (var oxygenTank in oxygenTanks) { 
            if (!oxygenTank.BlockDefinition.SubtypeId.Contains("Hydrogen")) { 
                oxygenTankFillLevel += oxygenTank.FilledRatio; 
                oxygenTankCount++; 
            } 
        } 
         
        oxygenTankFillLevel = Math.Round(oxygenTankFillLevel / oxygenTankCount * 100, 2); 
    } 
} 
 
 
// ======================================================================================= 
// Method for the main rotation logic 
// ======================================================================================= 
 
void rotationLogic() { 
    // If output is less than 10% of max detected output, it's night time 
    if (maxOutputAP <= maxDetectedOutputAP * 0.1) { 
        informationString +="Night Mode.\n"; 
         
        // Rotate the panels to the base angle or stop them 
        if (rotateToSunrise) { 
            foreach (var rotor in rotors) { 
                writeCustomData(rotor, "firstLockOfDay", 1); 
            } 
            if (manualAngle) { 
                rotateAll(manualAngleHorizontal, manualAngleVertical); 
            } else { 
                rotateAllToSunrise(); 
            } 
        } else { 
            stopAll(); 
        } 
     
    // If output is measured, start rotating 
    } else { 
        // Counter variables for the currently moving rotors 
        int VrotorMoving = 0; 
        int HrotorMoving = 0; 
         
        // Vertical rotors 
        foreach (var Vrotor in Vrotors) { 
            double output = readCustomData(Vrotor, "output"); 
            double outputLast = readCustomData(Vrotor, "outputLast"); 
            double outputLocked = readCustomData(Vrotor, "outputLocked"); 
            double direction = readCustomData(Vrotor, "direction"); 
            double directionChanged = readCustomData(Vrotor, "directionChanged"); 
            double directionTimer = readCustomData(Vrotor, "directionTimer"); 
            double allowRotation = readCustomData(Vrotor, "allowRotation"); 
            double timeSinceRotation = readCustomData(Vrotor, "timeSinceRotation"); 
             
            // Only move the rotor, if the output is 1% below or above the last locked output and it's allowed to rotate 
            if ((output <= outputLocked * 0.99 || output >= outputLocked * 1.01) && allowRotation == 1 && timeSinceRotation >= 10) { 
                // Disallow rotation for the Hrotors on the of the Vrotor 
                setAllowRotationH(Vrotor, false); 
                outputLocked = 0; 
                 
                // Check if the output goes down to reverse the rotation 
                if (output <= outputLast && directionTimer == 2 && directionChanged == 0) { 
                    direction = -direction; 
                    directionTimer = 0; 
                    directionChanged = 1; 
                } 
                 
                // Turn the rotor 
                rotate(Vrotor, direction); 
                 
                // If the output reached maximum or is zero, stop the rotor 
                if (output < outputLast && directionTimer >= 3 || output == 0) { 
                    // Stop the rotor and allow the Hrotor to rotate 
                    stop(Vrotor); 
                    setAllowRotationH(Vrotor, true); 
                     
                    // If this is the first lock of the day and rotateToSunrise is true, store the angle 
                    if (rotateToSunrise && readCustomData(Vrotor, "firstLockOfDay") == 1) { 
                        writeCustomData(Vrotor, "firstLockOfDay", 0); 
                        writeCustomData(Vrotor, "sunriseAngle", Math.Round(Vrotor.Angle * (180.0 / Math.PI))); 
                    } 
                     
                    outputLocked = output; 
                    directionChanged = 0; 
                    directionTimer = 0; 
                    timeSinceRotation = 0; 
                } else { 
                    VrotorMoving++; 
                    directionTimer++; 
                } 
                 
                // Update outputLocked, direction, directionChanged, directionTimer and timeSinceRotation on the rotor 
                writeCustomData(Vrotor, "outputLocked", outputLocked); 
                writeCustomData(Vrotor, "direction", direction); 
                writeCustomData(Vrotor, "directionChanged", directionChanged); 
                writeCustomData(Vrotor, "directionTimer", directionTimer); 
                writeCustomData(Vrotor, "timeSinceRotation", timeSinceRotation); 
            } else { 
                // Stop the rotor and allow the Hrotor and itself to rotate 
                stop(Vrotor); 
                setAllowRotationH(Vrotor, true); 
                writeCustomData(Vrotor, "allowRotation", 1); 
                 
                // Update directionChanged, directionTimer and timeSinceRotation on the rotor 
                directionChanged = 0; 
                directionTimer = 0; 
                timeSinceRotation++; 
                writeCustomData(Vrotor, "directionChanged", directionChanged); 
                writeCustomData(Vrotor, "directionTimer", directionTimer); 
                writeCustomData(Vrotor, "timeSinceRotation", timeSinceRotation); 
            } 
        } 
         
        // Horizontal rotors 
        foreach (var Hrotor in Hrotors) { 
            double output = readCustomData(Hrotor, "output"); 
            double outputLast = readCustomData(Hrotor, "outputLast"); 
            double outputLocked = readCustomData(Hrotor, "outputLocked"); 
            double direction = readCustomData(Hrotor, "direction"); 
            double directionChanged = readCustomData(Hrotor, "directionChanged"); 
            double directionTimer = readCustomData(Hrotor, "directionTimer"); 
            double allowRotation = readCustomData(Hrotor, "allowRotation"); 
            double timeSinceRotation = readCustomData(Hrotor, "timeSinceRotation"); 
             
            // Only move the rotor, if the output is 1% below or above the last locked output and it's allowed to rotate 
            if ((output <= outputLocked * 0.99 || output >= outputLocked * 1.01) && allowRotation == 1 && timeSinceRotation >= 10) { 
                // Disallow rotation for the Vrotor below the Hrotor 
                setAllowRotationV(Hrotor, false); 
                outputLocked = 0; 
                 
                // Check if the output goes down to reverse the rotation 
                if (output <= outputLast && directionTimer == 2 && directionChanged == 0) { 
                    direction = -direction; 
                    directionTimer = 0; 
                    directionChanged = 1; 
                } 
                 
                // Turn the rotor 
                rotate(Hrotor, direction); 
                 
                // If the output reached maximum or is zero, force lock 
                if (output < outputLast && directionTimer >= 3 || output == 0) { 
                    // Stop the rotor 
                    stop(Hrotor); 
                     
                    // If this is the first lock of the day and rotateToSunrise is true, store the angle 
                    if (rotateToSunrise && readCustomData(Hrotor, "firstLockOfDay") == 1) { 
                        writeCustomData(Hrotor, "firstLockOfDay", 0); 
                        writeCustomData(Hrotor, "sunriseAngle", Math.Round(Hrotor.Angle * (180.0 / Math.PI))); 
                    } 
                                         
                    outputLocked = output; 
                    directionChanged = 0; 
                    directionTimer = 0; 
                    timeSinceRotation = 0; 
                } else { 
                    HrotorMoving++; 
                    directionTimer++; 
                } 
                 
                // Update outputLocked, direction, directionChanged, directionTimer and timeSinceRotation on the rotor 
                writeCustomData(Hrotor, "outputLocked", outputLocked); 
                writeCustomData(Hrotor, "direction", direction); 
                writeCustomData(Hrotor, "directionChanged", directionChanged); 
                writeCustomData(Hrotor, "directionTimer", directionTimer); 
                writeCustomData(Hrotor, "timeSinceRotation", timeSinceRotation); 
            } else { 
                // Stop the rotor 
                stop(Hrotor); 
                 
                // Update directionChanged, directionTimer and timeSinceRotation on the rotor 
                directionChanged = 0; 
                directionTimer = 0; 
                timeSinceRotation++; 
                writeCustomData(Hrotor, "directionChanged", directionChanged); 
                writeCustomData(Hrotor, "directionTimer", directionTimer); 
                writeCustomData(Hrotor, "timeSinceRotation", timeSinceRotation); 
            } 
        } 
         
        // Creat the information about the moving rotors 
        if (VrotorMoving == 0 && HrotorMoving == 0) { 
            informationString += "Aligned.\n"; 
        } else if (VrotorMoving == 0) { 
            informationString += "Aligning " + HrotorMoving + " horizontal rotors..\n"; 
        } else if (HrotorMoving == 0) { 
            informationString += "Aligning " + VrotorMoving + " vertical rotors..\n"; 
        } else { 
            informationString += "Aligning " + HrotorMoving + " horizontal and " + VrotorMoving + " vertical rotors..\n"; 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method for allowing Vrotors to rotate or not, input should be a Hrotor on top of the Vrotor 
// ======================================================================================= 
  
void setAllowRotationV(IMyMotorStator rotor, bool value) { 
    foreach (var Vrotor in Vrotors){ 
        if (rotor.CubeGrid == Vrotor.TopGrid) { 
            if (value) { 
                writeCustomData(Vrotor, "allowRotation", 1); 
            } else { 
                writeCustomData(Vrotor, "allowRotation", 0); 
            } 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method for allowing Hrotors to rotate or not, input should be a Vrotor on below the Hrotor 
// ======================================================================================= 
  
void setAllowRotationH(IMyMotorStator rotor, bool value) { 
    foreach (var Hrotor in Hrotors){ 
        if (rotor.TopGrid == Hrotor.CubeGrid) { 
            if (value) { 
                writeCustomData(Hrotor, "allowRotation", 1); 
            } else { 
                writeCustomData(Hrotor, "allowRotation", 0); 
            } 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method for rotating a rotor in a specific direction 
// ======================================================================================= 
  
void rotate(IMyMotorStator rotor, double direction, float speed = rotorSpeed) { 
    rotor.SetValueBool("Force weld", false); 
    rotor.SetValue("Velocity", speed * Convert.ToSingle(direction)); 
} 
         
 
// ======================================================================================= 
// Method for stopping a specific rotor 
// ======================================================================================= 
  
void stop(IMyMotorStator rotor) { 
    rotor.SetValueBool("Force weld", true); 
    rotor.SetValue("Velocity", 0f); 
} 
 
 
// ======================================================================================= 
// Method for setting a specific rotor angle 
// ======================================================================================= 
  
void rotateAll(int horizontalAngle = 90, int verticalAngle = 90) { 
    // Check variable for vertical rotors 
    bool HrotationDone = true; 
    rotationDone = true; 
     
    // Counter variables for the currently moving rotors 
    int VrotorMoving = 0; 
    int HrotorMoving = 0; 
     
    // Temporary information string which gets added to the global information string at the end 
    string tempInformationString = ""; 
     
    // Horizontal rotors 
    foreach (var Hrotor in Hrotors) { 
        int HrotorAngle = (int)Math.Round(Hrotor.Angle * (180.0 / Math.PI)); 
        int targetAngle = horizontalAngle; 
         
        // Rotor angle correction 
        if (Hrotor.CustomName.IndexOf("[90]") >= 0) { 
            targetAngle += 90; 
        } else if (Hrotor.CustomName.IndexOf("[180]") >= 0) { 
            targetAngle += 180; 
        } else if (Hrotor.CustomName.IndexOf("[270]") >= 0) { 
            targetAngle += 270; 
        } 
        if (targetAngle >= 360) targetAngle -= 360; 
         
        // Invert rotorangle if rotor is facing forward, up or right 
        bool invert = false; 
        if (Hrotor.Orientation.Up.ToString() == "Up") { 
            invert = true; 
        } else if (Hrotor.Orientation.Up.ToString() == "Forward") { 
            invert = true; 
        } else if (Hrotor.Orientation.Up.ToString() == "Right") { 
            invert = true; 
        } 
         
        // If rotor has limits, limit the targetAngle too 
        if (!float.IsInfinity(Hrotor.GetValueFloat("UpperLimit"))) { 
            if (invert) targetAngle = -targetAngle; 
            if (targetAngle > (int)Hrotor.GetValueFloat("UpperLimit")) { 
                targetAngle = (int)Hrotor.GetValueFloat("UpperLimit"); 
            } 
            if (targetAngle < (int)Hrotor.GetValueFloat("LowerLimit")) { 
                targetAngle = (int)Hrotor.GetValueFloat("LowerLimit"); 
            } 
        } else { 
            if (invert) targetAngle = 360 - targetAngle; 
        } 
         
        // If angle is correct, stop the rotor 
        if (HrotorAngle == targetAngle) { 
            stop(Hrotor); 
             
            // Reset rotation direction 
            if (invert) { 
                writeCustomData(Hrotor, "direction", -1); 
            } else { 
                writeCustomData(Hrotor, "direction", 1); 
            } 
             
            // Reset timeSinceRotation 
            writeCustomData(Hrotor, "timeSinceRotation", 1); 
             
        // Else move the rotor 
        } else { 
            HrotationDone = false; 
            rotationDone = false; 
            HrotorMoving++; 
             
            // Figure out the shortest rotation direction 
            int direction = 1; 
            if (HrotorAngle > targetAngle) { 
                direction = -1; 
            } 
            if (HrotorAngle <= 90 && targetAngle >= 270) { 
                direction = -1; 
            } 
            if (HrotorAngle >= 270 && targetAngle <= 90) { 
                direction = 1; 
            } 
          
            // Move rotor 
            Single speed = rotorSpeed; 
            if (Math.Abs(HrotorAngle - targetAngle) > 15) speed = rotorSpeedFast; 
            if (Math.Abs(HrotorAngle - targetAngle) < 3) speed = 0.05f; 
            rotate(Hrotor, direction, speed); 
             
            // Create information 
            tempInformationString = HrotorMoving + " horizontal rotors are set to " + horizontalAngle + "°\n"; 
        } 
    } 
     
    // Vertical rotors 
    foreach (var Vrotor in Vrotors) { 
        int VrotorAngle = (int)Math.Round(Vrotor.Angle * (180.0 / Math.PI)); 
        int targetAngle = verticalAngle; 
         
        // Rotor angle correction 
        if (Vrotor.CustomName.IndexOf("[90]") >= 0) { 
            targetAngle += 90; 
        } else if (Vrotor.CustomName.IndexOf("[180]") >= 0) { 
            targetAngle += 180; 
        } else if (Vrotor.CustomName.IndexOf("[270]") >= 0) { 
            targetAngle += 270; 
        } 
        if (targetAngle >= 360) targetAngle -= 360; 
         
        // If rotor has limits, limit the targetAngle too 
        if (!float.IsInfinity(Vrotor.GetValueFloat("UpperLimit"))) { 
            if (targetAngle > (int)Vrotor.GetValueFloat("UpperLimit")) { 
                targetAngle = (int)Vrotor.GetValueFloat("UpperLimit"); 
            } 
        } 
                 
        // If angle is correct or horizontal rotors are still spinning, stop the rotor 
        if (VrotorAngle == targetAngle || HrotationDone == false) { 
            stop(Vrotor); 
             
            // Reset rotation direction 
            writeCustomData(Vrotor, "direction", 1); 
             
            // Reset timeSinceRotation 
            writeCustomData(Vrotor, "timeSinceRotation", 0); 
             
        // Else move the rotor 
        } else { 
            rotationDone = false; 
            VrotorMoving++; 
             
            // Figure out the shortest rotation direction 
            int direction = 1; 
            if (VrotorAngle > targetAngle) { 
                direction = -1; 
            } 
            if (VrotorAngle <= 90 && targetAngle >= 270) { 
                direction = -1; 
            } 
            if (VrotorAngle >= 270 && targetAngle <= 90) { 
                direction = 1; 
            } 
          
            // Move rotor 
            Single speed = rotorSpeed; 
            if (Math.Abs(VrotorAngle - targetAngle) > 15) speed = rotorSpeedFast; 
            if (Math.Abs(VrotorAngle - targetAngle) < 3) speed = 0.05f; 
            rotate(Vrotor, direction, speed); 
             
            // Create information 
            tempInformationString = VrotorMoving + " vertical rotors are set to " + verticalAngle + "°\n"; 
        } 
    } 
     
    // Append the temporary information string to the global information string 
    informationString += tempInformationString; 
} 
 
 
// ======================================================================================= 
// Method for stopping all rotors, never call before initialize() 
// ======================================================================================= 
 
void stopAll() { 
    foreach (var rotor in rotors) { 
        rotor.SetValue("Velocity", 0f); 
        rotor.SetValueBool("Force weld", true); 
    } 
} 
 
 
// ======================================================================================= 
// Method for rotating all panels to the sunrise location 
// ======================================================================================= 
  
void rotateAllToSunrise() { 
    // Check variable for vertical rotors 
    bool HrotationDone = true; 
    rotationDone = true; 
     
    // Counter variables for the currently moving rotors 
    int VrotorMoving = 0; 
    int HrotorMoving = 0; 
     
    // Temporary information string which gets added to the global information string at the end 
    string tempInformationString = ""; 
     
    // Horizontal rotors 
    foreach (var Hrotor in Hrotors) { 
        double HrotorAngle = Math.Round(Hrotor.Angle * (180.0 / Math.PI)); 
        double targetAngle = readCustomData(Hrotor, "sunriseAngle"); 
         
        // Invert rotation if rotor is facing forward, up or right 
        bool invert = false; 
        if (Hrotor.Orientation.Up.ToString() == "Up") { 
            invert = true; 
        } else if (Hrotor.Orientation.Up.ToString() == "Forward") { 
            invert = true; 
        } else if (Hrotor.Orientation.Up.ToString() == "Right") { 
            invert = true; 
        } 
         
        // If rotor has limits, limit the targetAngle too 
        if (!float.IsInfinity(Hrotor.GetValueFloat("UpperLimit"))) { 
            if (targetAngle > Hrotor.GetValueFloat("UpperLimit")) { 
                targetAngle = Hrotor.GetValueFloat("UpperLimit"); 
            } 
            if (targetAngle < Hrotor.GetValueFloat("LowerLimit")) { 
                targetAngle = Hrotor.GetValueFloat("LowerLimit"); 
            } 
        } 
         
        // If angle is correct, stop the rotor 
        if (HrotorAngle == targetAngle) { 
            stop(Hrotor); 
             
            // Reset rotation direction 
            if (invert) { 
                writeCustomData(Hrotor, "direction", -1); 
            } else { 
                writeCustomData(Hrotor, "direction", 1); 
            } 
             
            // Reset timeSinceRotation 
            writeCustomData(Hrotor, "timeSinceRotation", 1); 
             
        // Else move the rotor 
        } else { 
            HrotationDone = false; 
            rotationDone = false; 
            HrotorMoving++; 
             
            // Figure out the shortest rotation direction 
            int direction = 1; 
            if (HrotorAngle > targetAngle) { 
                direction = -1; 
            } 
            if (HrotorAngle <= 90 && targetAngle >= 270) { 
                direction = -1; 
            } 
            if (HrotorAngle >= 270 && targetAngle <= 90) { 
                direction = 1; 
            } 
          
            // Move rotor 
            Single speed = rotorSpeed; 
            if (Math.Abs(HrotorAngle - targetAngle) > 15) speed = rotorSpeedFast; 
            if (Math.Abs(HrotorAngle - targetAngle) < 3) speed = 0.05f; 
            rotate(Hrotor, direction, speed); 
             
            // Create information 
            tempInformationString = HrotorMoving + " horizontal rotors are set to sunrise position\n"; 
        } 
    } 
     
    // Vertical rotors 
    foreach (var Vrotor in Vrotors) { 
        double VrotorAngle = Math.Round(Vrotor.Angle * (180.0 / Math.PI)); 
        double targetAngle = readCustomData(Vrotor, "sunriseAngle"); 
         
        // Rotor angle correction 
        if (Vrotor.CustomName.IndexOf("[90]") >= 0) { 
            targetAngle += 90; 
        } else if (Vrotor.CustomName.IndexOf("[180]") >= 0) { 
            targetAngle += 180; 
        } else if (Vrotor.CustomName.IndexOf("[270]") >= 0) { 
            targetAngle += 270; 
        } 
        if (targetAngle >= 360) targetAngle -= 360; 
         
        // If rotor has limits, limit the targetAngle too 
        if (!float.IsInfinity(Vrotor.GetValueFloat("UpperLimit"))) { 
            if (targetAngle > Vrotor.GetValueFloat("UpperLimit")) { 
                targetAngle = Vrotor.GetValueFloat("UpperLimit"); 
            } 
        } 
                 
        // If angle is correct or horizontal rotors are still spinning, stop the rotor 
        if (VrotorAngle == targetAngle || HrotationDone == false) { 
            stop(Vrotor); 
             
            // Reset rotation direction 
            writeCustomData(Vrotor, "direction", 1); 
             
            // Reset timeSinceRotation 
            writeCustomData(Vrotor, "timeSinceRotation", 0); 
             
        // Else move the rotor 
        } else { 
            rotationDone = false; 
            VrotorMoving++; 
             
            // Figure out the shortest rotation direction 
            int direction = 1; 
            if (VrotorAngle > targetAngle) { 
                direction = -1; 
            } 
            if (VrotorAngle <= 90 && targetAngle >= 270) { 
                direction = -1; 
            } 
            if (VrotorAngle >= 270 && targetAngle <= 90) { 
                direction = 1; 
            } 
          
            // Move rotor 
            Single speed = rotorSpeed; 
            if (Math.Abs(VrotorAngle - targetAngle) > 15) speed = rotorSpeedFast; 
            if (Math.Abs(VrotorAngle - targetAngle) < 3) speed = 0.05f; 
            rotate(Vrotor, direction, speed); 
             
            // Create information 
            tempInformationString = VrotorMoving + " vertical rotors are set to sunrise position\n"; 
        } 
    } 
     
    // Append the temporary information string to the global information string 
    informationString += tempInformationString; 
} 
 
 
// ======================================================================================= 
// Method for calculating percentage of two values 
// ======================================================================================= 
 
double percent(double numerator, double denominator) { 
    double percentage = Math.Round(numerator / denominator * 100, 1); 
    if (double.IsNaN(percentage)) { 
        return 0; 
    } else { 
        return percentage; 
    } 
} 
 
 
// ======================================================================================= 
// Method for getting a value from a battery detailed info, given a line 
// ======================================================================================= 
 
double getBatteryValues(IMyBatteryBlock battery, int line) { 
    string[] batteryDetails = battery.DetailedInfo.Split('\n'); 
    string unit = "MW"; 
     
    batteryDetails[line] = batteryDetails[line].Remove(0, batteryDetails[line].IndexOf(":") + 1); 
    batteryDetails[line] = batteryDetails[line].TrimStart(' '); 
    unit = batteryDetails[line].Substring(batteryDetails[line].IndexOf(" ") + 1, 2); 
    batteryDetails[line] = batteryDetails[line].Substring(0, batteryDetails[line].IndexOf(" ")); 
     
    double value = 0; 
    Double.TryParse(batteryDetails[line], out value); 
     
    if (unit == "kW") value /= 1000; 
    if (unit == "GW") value *= 1000; 
     
    return value; 
} 
                             
 
 
// ======================================================================================= 
// Method for creating the information string 
// ======================================================================================= 
 
void createInformation() { 
    informationString += "\n"; 
     
    // Solar Panels 
    informationString += "Statistics for " + solarPanelsCount + " Solar Panels:\n"; 
    informationString += "Max. Output: " + maxOutputAPString + " / " + maxDetectedOutputAPString + " (" + percent(maxOutputAP, maxDetectedOutputAP) + "%)\n"; 
    informationString += "Current Output: " + currentOutputAPString + " / " + maxDetectedOutputAPString + " (" + percent(currentOutputAP, maxDetectedOutputAP) + "%)\n"; 
    informationString += "Max. used Output: " + maxUsedOutputString + " / " + maxDetectedOutputAPString + " (" + percent(maxUsedOutputAP, maxDetectedOutputAP) + "%)\n\n"; 
     
    // Batteries 
    if (batteriesCount > 0 && showBatteryStats) { 
        informationString += "Statistics for " + batteriesCount + " Batteries:\n"; 
        informationString += "Current Input: " + batteriesCurrentInput + " MW / " + batteriesMaxInput + " MW (" + percent(batteriesCurrentInput, batteriesMaxInput) + "%)\n"; 
        informationString += "Current Output: " + batteriesCurrentOutput + " MW / " + batteriesMaxOutput + " MW (" + percent(batteriesCurrentOutput, batteriesMaxOutput) + "%)\n"; 
        informationString += "Stored Power: " + batteriesPower + " MWh / " + batteriesMaxPower + " MWh (" + percent(batteriesPower, batteriesMaxPower) + "%)\n\n"; 
    } 
     
    // Oxygen Farms 
    if (showOxygenStats && (oxygenFarmCount > 0 || oxygenTankCount > 0)) { 
        informationString += "Statistics for Oxygen:\n"; 
        if (oxygenFarmCount > 0) { 
            informationString += "Oxygen Farms: " + oxygenFarmCount + ", Efficiency: " + oxygenFarmEfficiency + "%\n"; 
        } 
         
        if (oxygenTankCount > 0) { 
            informationString += "Oxygen Tanks: " + oxygenTankCount + ", Fill Level: " + oxygenTankFillLevel + "%\n"; 
        } 
        informationString += "\n"; 
    } 
     
    // Location time and Base Light Management 
    if (showLocationTime) { 
        if (dayTimer == dayLength || sunSet == 0) { 
            informationString += "Calculating location time.. Takes a full day/night cycle..\n"; 
            informationString += "Sunrise: " + dayTimer + " seconds ago\n"; 
            if (sunSet == 0) { 
                informationString += "Sunset: unknown\n"; 
            } else { 
                informationString += "Sunset: " + sunSet + " seconds after sunrise\n"; 
            } 
        } else { 
            informationString += "Location time: " + getTimeString(dayTimer) + "\n"; 
            if (!baseLightManagement) { 
                informationString += "Sunrise: " + getTimeString(dayLength) + "\n"; 
                informationString += "Sunset: " + getTimeString(sunSet) + "\n"; 
            } else { 
                informationString += "Sunrise: " + getTimeString(dayLength) + " (Light off: " + lightOffHour + ":00) \n"; 
                informationString += "Sunset: " + getTimeString(sunSet) + " (Light on: " + lightOnHour + ":00) \n"; 
            } 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method for Regular-LCD Output 
// ======================================================================================= 
 
void writeLCD() { 
    // Only use this function if there are LCDs 
    if (regularLcds.Length > 0) { 
          
        // Create a new List for the LCDs 
        var lcds = new List<IMyTextPanel>(); 
                
        // Cycle through all the items in regularLcds to find groups or LCDs     
        foreach (var item in regularLcds) { 
            // If the item is a group, get the LCDs and join the list with lcds list 
            var lcdGroup = GridTerminalSystem.GetBlockGroupWithName(item); 
            if (lcdGroup != null) { 
                var tempLcds = new List<IMyTextPanel>(); 
                lcdGroup.GetBlocksOfType<IMyTextPanel>(tempLcds); 
                lcds.AddRange(tempLcds); 
            // Else try adding a single LCD 
            } else { 
                IMyTextPanel regularLcd = GridTerminalSystem.GetBlockWithName(item) as IMyTextPanel; 
                if (regularLcd == null) { 
                    warning += "LCD not found:\n'" + item + "'\n\n"; 
                } else { 
                    lcds.Add(regularLcd); 
                } 
            } 
        } 
         
        // Figure out the amount of lines for scrolling content 
        var lines = informationString.Split('\n'); 
        informationString = ""; 
         
        if (lines.Length > 24) { 
            if (scrollWait > 0) scrollWait--; 
            if (scrollWait <= 0) lineStart += scrollDirection; 
             
            if (lineStart + 20 >= lines.Length && scrollWait <= 0) { 
                scrollDirection = -1; 
                scrollWait = 3; 
            } 
            if (lineStart <= 3 && scrollWait <= 0) { 
                scrollDirection = 1; 
                scrollWait = 3; 
            } 
        } else { 
            lineStart = 3; 
            scrollDirection = 1; 
            scrollWait = 3; 
        } 
         
        // Always create header 
        for (var line = 0; line < 3; line++) { 
            informationString += lines[line] + "\n"; 
        } 
         
        // Create scrolling content based on the starting line 
        for (var line = lineStart; line < lines.Length; line++) { 
            informationString += lines[line] + "\n"; 
        } 
           
        foreach (var lcd in lcds) { 
            // Print contents to its public text 
            lcd.WritePublicText(informationString, false); 
            lcd.SetValue("FontSize", 0.8f); 
            lcd.ShowPublicTextOnScreen(); 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method for Corner-LCD Output 
// ======================================================================================= 
  
void writeCornerLCD() { 
// Only use this function if there are corner LCDs 
    if (cornerLcds.Length > 0) { 
         
        // Create a new List for the LCDs 
        var lcds = new List<IMyTextPanel>(); 
                    
        // Cycle through all the items in cornerLcds to find groups or corner LCDs     
        foreach (var item in cornerLcds) { 
            // If the item is a group, get the LCDs and join the list with lcds list 
            var lcdGroup = GridTerminalSystem.GetBlockGroupWithName(item); 
            if (lcdGroup != null) { 
                var tempLcds = new List<IMyTextPanel>(); 
                lcdGroup.GetBlocksOfType<IMyTextPanel>(tempLcds); 
                lcds.AddRange(tempLcds); 
            // Else try adding a single corner LCD 
            } else { 
                IMyTextPanel cornerLcd = GridTerminalSystem.GetBlockWithName(item) as IMyTextPanel; 
                if (cornerLcd == null) { 
                    warning += "Corner-LCD not found:\n'" + item + "'\n\n"; 
                } else { 
                    lcds.Add(cornerLcd); 
                } 
            } 
        } 
          
        foreach (var lcd in lcds) { 
            // Prepare the text based on the custom data of the panel 
            string cornerLcdText = ""; 
            if (lcd.CustomData == "time" && showLocationTime) { 
                cornerLcdText += "\n"; 
                for (int a = 0; a <= 35; a++) cornerLcdText += " ";  
                cornerLcdText += getTimeString(dayTimer); 
            } else if (lcd.CustomData == "battery" && showBatteryStats) { 
                cornerLcdText += "Statistics for " + batteriesCount + " Batteries:\n"; 
                cornerLcdText += "Current I/O: " + batteriesCurrentInput + " MW in, " + batteriesCurrentOutput + " MW out\n"; 
                cornerLcdText += "Stored Power: " + batteriesPower + " MWh / " + batteriesMaxPower + " MWh (" + percent(batteriesPower, batteriesMaxPower) + "%)\n\n"; 
            } else if (lcd.CustomData == "oxygen" && showOxygenStats) { 
                cornerLcdText += "Statistics for Oxygen:\n"; 
                if (oxygenFarmCount > 0) { 
                    cornerLcdText += "Oxygen Farms: " + oxygenFarmCount + ", Efficiency: " + oxygenFarmEfficiency + "%\n"; 
                } 
                if (oxygenTankCount > 0) { 
                    cornerLcdText += "Oxygen Tanks: " + oxygenTankCount + ", Fill Level: " + oxygenTankFillLevel + "%\n"; 
                } 
            } else {             
                cornerLcdText += "Statistics for " + solarPanelsCount + " Solar Panels:\n"; 
                cornerLcdText += "Max. Output: " + maxOutputAPString + " / " + maxDetectedOutputAPString + " (" + percent(maxOutputAP, maxDetectedOutputAP) + "%)\n"; 
                cornerLcdText += "Current Output: " + currentOutputAPString + " / " + maxDetectedOutputAPString + " (" + percent(currentOutputAP, maxDetectedOutputAP) + "%)\n"; 
            } 
         
            // Print contents to its public text 
            lcd.WritePublicText(cornerLcdText, false); 
            lcd.SetValue("FontSize", 0.9f); 
            lcd.ShowPublicTextOnScreen(); 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method time calculation (required for base light management) 
// ======================================================================================= 
 
void timeCalculation() { 
    // Continous day timer in seconds 
    dayTimer += 1; 
    safetyTimer += 1; 
 
    // Failsafe for day timer if no day / night cycle could be measured after 10 hours 
    if (dayTimer > 36000) { 
        dayTimer = 0; 
        safetyTimer = 0; 
        dayLength = 0; 
    } 
 
    // Detect maximum day duration 
    if (dayLength < dayTimer) dayLength = dayTimer; 
 
    // Detect sunset 
    if (maxOutputAP < maxDetectedOutputAP * nightDetectionMultiplier && maxOutputAPLast >= maxDetectedOutputAP * nightDetectionMultiplier && safetyTimer > 120) { 
        sunSet = dayTimer; 
        safetyTimer = 0; 
    } 
  
    // Reset day timer (sunrise) 
    if (maxOutputAP > maxDetectedOutputAP * nightDetectionMultiplier && maxOutputAPLast <= maxDetectedOutputAP * nightDetectionMultiplier && safetyTimer > 120) { 
        dayTimer = 0; 
        safetyTimer = 0; 
    } 
     
    // Save variables into the programmable block's custom data field 
    string customData = ""; 
    customData += "dayTimer=" + dayTimer + ";\n"; 
    customData += "safetyTimer=" + safetyTimer + ";\n"; 
    customData += "dayLength=" + dayLength + ";\n"; 
    customData += "sunSet=" + sunSet + ";\n"; 
    customData += "outputLast=" + maxOutputAPLast + ";\n"; 
    Me.CustomData = customData; 
} 
 
 
// ======================================================================================= 
// Method for returning a time string based on a number of seconds, returns string 
// ======================================================================================= 
 
string getTimeString(double timeToEvaluate, bool returnHour = false) { 
    string timeString = ""; 
  
    // Calculate Midnight 
    midNight = sunSet + (dayLength - sunSet) / 2; 
       
    // Calculate Time 
    double hourLength = dayLength / 24D; 
    double time; 
    if (timeToEvaluate < midNight) { 
        time = (timeToEvaluate + (dayLength - midNight)) / hourLength; 
    } else { 
        time = (timeToEvaluate - midNight) / hourLength; 
    } 
  
    double timeHour = Math.Floor(time); 
    double timeMinute = Math.Floor((time % 1 * 100) * 0.6); 
    string timeMinuteString = "" + timeMinute; 
    if (timeMinute < 10) timeMinuteString = "0" + timeMinute; 
  
    timeString = timeHour + ":" + timeMinuteString; 
     
    if (returnHour) { 
        return timeHour.ToString(); 
    } else { 
        return timeString; 
    } 
} 
 
 
// ======================================================================================= 
// Method for base light management 
// ======================================================================================= 
   
void lightManagement() { 
    // Create a new List for the Lights 
    var lights = new List<IMyInteriorLight>(); 
    var spotLights = new List<IMyReflectorLight>(); 
       
    // If set, fill the list only with the group's lights 
    if (baseLightGroups.Length > 0) { 
        var tempLights = new List<IMyInteriorLight>(); 
        var tempSpotLights = new List<IMyReflectorLight>(); 
        foreach (var group in baseLightGroups) { 
            var lightGroup = GridTerminalSystem.GetBlockGroupWithName(group); 
            if (lightGroup != null) { 
                lightGroup.GetBlocksOfType<IMyInteriorLight>(tempLights); 
                lights.AddRange(tempLights); 
                lightGroup.GetBlocksOfType<IMyReflectorLight>(tempSpotLights); 
                spotLights.AddRange(tempSpotLights); 
            } else { 
                warning += "Light group not found:\n'" + group + "'\n\n"; 
            } 
        } 
       
    // Else search for all interior lights and spotlights and fill the groups 
    } else { 
        GridTerminalSystem.GetBlocksOfType<IMyInteriorLight>(lights); 
        GridTerminalSystem.GetBlocksOfType<IMyReflectorLight>(spotLights); 
    } 
     
    int hour = int.Parse(getTimeString(dayTimer, true)); 
     
    // Toggle all interior lights based on time 
    foreach (var light in lights) { 
        // Toggle the lights on if it is night or off if it is day based on the hour or by comparing the current output 
        if (dayTimer != dayLength && hour >= lightOffHour && hour < lightOnHour) { 
            light.ApplyAction("OnOff_Off"); 
        } else if (dayTimer == dayLength && maxOutputAP > maxDetectedOutputAP * nightDetectionMultiplier) { 
            light.ApplyAction("OnOff_Off"); 
        } else { 
            light.ApplyAction("OnOff_On"); 
        } 
    } 
     
    // Toggle all spotlights based on time 
    foreach (var spotLight in spotLights) { 
        // Toggle the lights on if it is night or off if it is day based on the hour or by comparing the current output 
        if (dayTimer != dayLength && hour >= lightOffHour && hour < lightOnHour) { 
            spotLight.ApplyAction("OnOff_Off"); 
        } else if (dayTimer == dayLength && maxOutputAP > maxDetectedOutputAP * nightDetectionMultiplier) { 
            spotLight.ApplyAction("OnOff_Off"); 
        } else { 
            spotLight.ApplyAction("OnOff_On"); 
        } 
    } 
} 
 
 
// ======================================================================================= 
// Method for triggering an external timer block 
// ======================================================================================= 
 
void triggerExternalTimerBlock() { 
    // Error management 
    if (events.Length == 0) { 
        warning += "No events for triggering specified!\n\n"; 
    } else if (timers.Length == 0) { 
        warning += "No timers for triggering specified!\n\n"; 
    } else if (events.Length != timers.Length) { 
        warning += "Every event needs a timer block name!\n"; 
        warning += "Found " + events.Length + " events and " + timers.Length + " timers.\n\n"; 
    } else { 
        int timerToTrigger = -1; 
        string triggerEvent = ""; 
        int seconds; 
         
        // Cycle through each entry in events and check if the current conditions match the entry 
        for (int i = 0; i <= events.Length - 1; i++) { 
            if (events[i] == "sunrise" && dayTimer == 0) { 
                timerToTrigger = i; 
                triggerEvent = "sunrise"; 
            } else if (events[i] == "sunset" && dayTimer == sunSet) { 
                timerToTrigger = i; 
                triggerEvent = "sunset"; 
            } else if (int.TryParse(events[i], out seconds) == true && dayTimer % seconds == 0) { 
                timerToTrigger = i; 
                triggerEvent = seconds + " seconds"; 
            } else if (getTimeString(dayTimer) == events[i]) { 
                timerToTrigger = i; 
                triggerEvent = events[i]; 
            }             
        } 
         
        // Trigger the timer block if a event matches the current conditions 
        if (timerToTrigger >= 0) { 
            // Find the timer block 
            var timer = GridTerminalSystem.GetBlockWithName(timers[timerToTrigger]); 
     
            if (timer == null) { 
                warning += "External timer block not found:\n'" + timers[timerToTrigger] + "'\n\n"; 
               } else { 
                timer.ApplyAction("Start"); 
                informationString += "External timer triggered! Reason: " + triggerEvent + "\n"; 
               } 
        } 
    } 
} 
 
 
// =======================================================================================   
// Method for saving the time calculation on world close and recompile
// =======================================================================================  
 
public void Save() { 
    // Save variables into the programmable block's custom data field    
    string customData = "";    
    customData += "dayTimer=" + dayTimer + ";\n";    
    customData += "safetyTimer=" + 0 + ";\n";    
    customData += "dayLength=" + dayLength + ";\n";    
    customData += "sunSet=" + sunSet + ";\n";    
    customData += "outputLast=" + maxOutputAPLast + ";\n";    
    Me.CustomData = customData;  
}