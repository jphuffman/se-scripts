// Bovine's Turret Orientation Script
// Simple script to make turrets face the direction they were placed in

public void Main(string argument)
{
    // get all turrets with argument in their name
    List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
    GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(turrets, turret => turret.CustomName.ToLower().Contains(argument.ToLower()));

    // Set azimuth and elevation to 0
    foreach (IMyLargeTurretBase turret in turrets)
    {
        turret.Azimuth = 0;
        turret.Elevation = 0;
        turret.SyncAzimuth();
        turret.SyncElevation();
    }
}