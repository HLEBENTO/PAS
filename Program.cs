using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace PAS
{
partial class Program : MyGridProgram
{
// HELLBENT's Pilot Assistant System
// Version: 1.0.1 (29.08.2023 01:14)
// A special thanks to Renesco Rocketman for the inspiration.
//
// You can find the guide on YouTube and Steam.
// YouTube: https://youtu.be/- Coming Soon -
// [EN] Steam: https://steamcommunity.com/sharedfiles/filedetails/?id=3026120452
// [RU] Steam: https://steamcommunity.com/sharedfiles/filedetails/?id=3026128017
// Script itself: https://steamcommunity.com/sharedfiles/filedetails/?id=3026140530

//=============================================================
// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===
//=============================================================
#region Global
string WorkMode = "plane", LastWorkMode = "";
bool Init = true;

string[] LCD_TAG = { "PAS_", "TCAS_", "Nav_", "Avionics_", "Flights_" };
string RC_TAG = "Autopilot", Include_TAG = "Include";
string WB_TAG = "Waypoints";
string LG_TAG = "Landing Gear";
string LGIndicator_TAG = "LG_Indicator";
string SB_TAG = "PAS";

const double timeLimit = 0.1, blocksUpd = 10, errUpd = 15, iniUpd = 1;
double timeForUpdateAP = 0, timeForUpdateINI = 0, timeForUpdateBlocks = 0, timeToClearErrors = 0, timeForAlarmDelay = 0;
bool wasMainPartExecuted = false;

RuntimeProfiler RP;
LandingGear LG;
SoundBlock SB;
AutoPilot AP;
Transponder transponder;
DisplayScheduler displayScheduler;
ILS ils;

MyIni Ini = new MyIni();
public Program() { Initialize(); }
public void Initialize()
{
WorkMode = "plane"; LastWorkMode = ""; Init = true; wasMainPartExecuted = false; timeForUpdateAP = timeForUpdateINI = timeForUpdateBlocks = 0;
RP = null; LG = null; SB = null; AP = null; transponder = null; displayScheduler = null; ils = null;

ParseIni();
RP = new RuntimeProfiler(this);
SB = new SoundBlock(this, SB_TAG);
transponder = new Transponder(IGC, WorkMode, SB);
if (WorkMode == "plane")
{
	LG = new LandingGear(this, LG_TAG, LGIndicator_TAG, SB);
	AP = new AutoPilot(this, RC_TAG, WB_TAG, Include_TAG, Me, Ini, Runtime, transponder, LG, SB);
	displayScheduler = new DisplayScheduler(this, RP, LCD_TAG, LG_TAG, LGIndicator_TAG, WorkMode, AP, transponder, LG, SB, SB_TAG);
}
if (WorkMode == "ils")
{
	ils = new ILS(this, transponder);
	displayScheduler = new DisplayScheduler(this, RP, LCD_TAG, WorkMode, transponder, SB, SB_TAG);
}
Save();
Runtime.UpdateFrequency = UpdateFrequency.Update1;
}
#endregion
#region ini
public void ParseIni()
{
//ini parsing.
if (!Ini.TryParse(Me.CustomData))
{
	Me.CustomData = "";
	throw new Exception("Initialisation failed. CustomData has been cleared. Recompile the script.");
}
WorkMode = Ini.Get("Work Mode", "Plane or ILS").ToString(WorkMode);
WorkMode = WorkMode.ToLower();


if (Init) { Ini.Set("Work Mode", "Plane or ILS", WorkMode); LastWorkMode = WorkMode; Me.CustomData = Ini.ToString(); Init = false; return; }
if (WorkMode != LastWorkMode) Initialize();
SB_TAG = Ini.Get("Settings", "Sound Block Name").ToString(SB_TAG);
if (WorkMode == "plane")
{

	RC_TAG = Ini.Get("Settings", "Remote Control Name").ToString(RC_TAG);
	WB_TAG = Ini.Get("Settings", "Waypoints Block Name").ToString(WB_TAG);

	AP.RepeatRoute = Ini.Get("Autopilot Settings", "Repeat Route").ToBoolean(AP.RepeatRoute);
	AP.UseTCAS = Ini.Get("Autopilot Settings", "Use TCAS In Flight").ToBoolean(AP.UseTCAS);
	AP.UseLandTCAS = Ini.Get("Autopilot Settings", "Use TCAS On Landing").ToBoolean(AP.UseLandTCAS);
	AP.UseGPWS = Ini.Get("Autopilot Settings", "Use GPWS").ToBoolean(AP.UseGPWS);
	AP.UseLandGPWS = Ini.Get("Autopilot Settings", "Use GPWS On Landing").ToBoolean(AP.UseLandGPWS);
	SB.ServiceEnabled = Ini.Get("Autopilot Settings", "Use Warning System").ToBoolean(SB.ServiceEnabled);
	AP.AlwaysDampiners = Ini.Get("Autopilot Settings", "Dampeners Always Active").ToBoolean(AP.AlwaysDampiners);
	transponder.MaxDistToILSRunway = Ini.Get("Autopilot Settings", "Max Dist To ILS Runway").ToDouble(transponder.MaxDistToILSRunway);

	transponder.Priority = Ini.Get("Flight Settings", "Flight Priority").ToInt32(transponder.Priority);
	transponder.Channel = Ini.Get("Flight Settings", "Radio Channel").ToString(transponder.Channel);
	transponder.ILS_Channel = Ini.Get("Flight Settings", "ILS Channel").ToString(transponder.ILS_Channel);
	transponder.Callsign = Ini.Get("Flight Settings", "Flight Callsign").ToString(transponder.Callsign);

	LG.ServiceEnabled = Ini.Get("Aircraft Settings", "Use Subgrid Landing Gear").ToBoolean(LG.ServiceEnabled);
	AP.VRotate = Ini.Get("Aircraft Settings", "VRotate").ToDouble(AP.VRotate);
	AP.V2 = Ini.Get("Aircraft Settings", "V2").ToDouble(AP.V2);
	AP.MinLandVelocity = Ini.Get("Aircraft Settings", "Min landing Velocity").ToDouble(AP.MinLandVelocity);
	AP.MaxLandVelocity = Ini.Get("Aircraft Settings", "Landing Velocity").ToDouble(AP.MaxLandVelocity);
	AP.AirbrakeVelocity = Ini.Get("Aircraft Settings", "Max Landing Velocity").ToDouble(AP.AirbrakeVelocity);
	AP.AutopilotTimerName = Ini.Get("Aircraft Settings", "Basic Point Timer Name").ToString(AP.AutopilotTimerName);
	AP.BasicAPAltitude = Ini.Get("Aircraft Settings", "Basic Autopilot Altitude").ToInt32(AP.BasicAPAltitude);
	AP.BasicAPSpeed = Ini.Get("Aircraft Settings", "Basic Autopilot Speed").ToInt32(AP.BasicAPSpeed);
	AP.BasicWaitTime = Ini.Get("Aircraft Settings", "Basic Wait Time").ToInt32(AP.BasicWaitTime);
	AP.BasicLandingAngle = Ini.Get("Aircraft Settings", "Basic Landing Angle").ToDouble(AP.BasicLandingAngle);
	AP.MaxPitchAngle = Ini.Get("Aircraft Settings", "Max Pitch Angle").ToDouble(AP.MaxPitchAngle);
	AP.MaxRollAngle = Ini.Get("Aircraft Settings", "Max Roll Angle").ToDouble(AP.MaxRollAngle);
	AP.MaxPitchSpeed = Ini.Get("Aircraft Settings", "Max Pitch Speed").ToDouble(AP.MaxPitchSpeed);
	AP.MaxRollSpeed = Ini.Get("Aircraft Settings", "Max Roll Speed").ToDouble(AP.MaxRollSpeed);
	AP.MaxYawSpeed = Ini.Get("Aircraft Settings", "Max Yaw Speed").ToDouble(AP.MaxYawSpeed);
	AP.PitchSpeedMultiplier = Ini.Get("Aircraft Settings", "Pitch Speed Multiplier").ToDouble(AP.PitchSpeedMultiplier);
	AP.RollSpeedMultiplier = Ini.Get("Aircraft Settings", "Roll Speed Multiplier").ToDouble(AP.RollSpeedMultiplier);
	AP.YawSpeedMultiplier = Ini.Get("Aircraft Settings", "Yaw Speed Multiplier").ToDouble(AP.YawSpeedMultiplier);

	AP.WaypointNum = Ini.Get("Storage", "Current Waypoint").ToInt32(AP.WaypointNum);
	AP.Sealevel_Calibrate = Ini.Get("Storage", "Altitude Calibration").ToDouble(AP.Sealevel_Calibrate);
	AP.ApEngaged = Ini.Get("Storage", "Autopilot Enabled").ToBoolean(AP.ApEngaged);
	AP.CruiseEngaged = Ini.Get("Storage", "Cruise Control Enabled").ToBoolean(AP.CruiseEngaged);
	AP.CruiseSpeed = Ini.Get("Storage", "Cruise Saved Speed").ToDouble(AP.CruiseSpeed);
	AP.CruiseAltitude = Ini.Get("Storage", "Cruise Saved Alt").ToDouble(AP.CruiseAltitude);
}
if (WorkMode == "ils")
{
	SB.ServiceEnabled = Ini.Get("Airport Settings", "Use Sound Block").ToBoolean(SB.ServiceEnabled);
	transponder.Channel = Ini.Get("Airport Settings", "Radio Channel").ToString(transponder.Channel);
	transponder.ILS_Channel = Ini.Get("Airport Settings", "ILS Channel").ToString(transponder.ILS_Channel);
	transponder.Callsign = Ini.Get("Airport Settings", "Airport Forward Callsign").ToString(transponder.Callsign);
	transponder.Callsign2 = Ini.Get("Airport Settings", "Airport Backward Callsign").ToString(transponder.Callsign2);
	ils.StartBeaconTag = Ini.Get("Airport Settings", "Runway Start Beacon Tag").ToString(ils.StartBeaconTag);
	ils.StopBeaconTag = Ini.Get("Airport Settings", "Runway Stop Beacon Tag").ToString(ils.StopBeaconTag);
}
}
public void Save()
{
ParseIni();

Ini.Set("Work Mode", "Plane or ILS", WorkMode);
Ini.Set("Settings", "Sound Block Name", SB_TAG);
if (WorkMode == "plane")
{
	Ini.Set("Settings", "Remote Control Name", RC_TAG);
	Ini.Set("Settings", "Waypoints Block Name", WB_TAG);

	Ini.Set("Autopilot Settings", "Repeat Route", AP.RepeatRoute);
	Ini.Set("Autopilot Settings", "Use TCAS In Flight", AP.UseTCAS);
	Ini.Set("Autopilot Settings", "Use TCAS On Landing", AP.UseLandTCAS);
	Ini.Set("Autopilot Settings", "Use GPWS", AP.UseGPWS);
	Ini.Set("Autopilot Settings", "Use GPWS On Landing", AP.UseLandGPWS);
	Ini.Set("Autopilot Settings", "Use Warning System", SB.ServiceEnabled);
	Ini.Set("Autopilot Settings", "Dampeners Always Active", AP.AlwaysDampiners);
	Ini.Set("Autopilot Settings", "Max Dist To ILS Runway", transponder.MaxDistToILSRunway);

	Ini.Set("Flight Settings", "Flight Priority", transponder.Priority);
	Ini.Set("Flight Settings", "Radio Channel", transponder.Channel);
	Ini.Set("Flight Settings", "ILS Channel", transponder.ILS_Channel);
	Ini.Set("Flight Settings", "Flight Callsign", transponder.Callsign);

	Ini.Set("Aircraft Settings", "Use Subgrid Landing Gear", LG.ServiceEnabled);
	Ini.Set("Aircraft Settings", "VRotate", AP.VRotate);
	Ini.Set("Aircraft Settings", "V2", AP.V2);
	Ini.Set("Aircraft Settings", "Min landing Velocity", AP.MinLandVelocity);
	Ini.Set("Aircraft Settings", "Landing Velocity", AP.MaxLandVelocity);
	Ini.Set("Aircraft Settings", "Max Landing Velocity", AP.AirbrakeVelocity);
	Ini.Set("Aircraft Settings", "Basic Point Timer Name", AP.AutopilotTimerName);
	Ini.Set("Aircraft Settings", "Basic Autopilot Altitude", AP.BasicAPAltitude);
	Ini.Set("Aircraft Settings", "Basic Autopilot Speed", AP.BasicAPSpeed);
	Ini.Set("Aircraft Settings", "Basic Wait Time", AP.BasicWaitTime);
	Ini.Set("Aircraft Settings", "Basic Landing Angle", AP.BasicLandingAngle);
	Ini.Set("Aircraft Settings", "Max Pitch Angle", AP.MaxPitchAngle);
	Ini.Set("Aircraft Settings", "Max Roll Angle", AP.MaxRollAngle);
	Ini.Set("Aircraft Settings", "Max Pitch Speed", AP.MaxPitchSpeed);
	Ini.Set("Aircraft Settings", "Max Roll Speed", AP.MaxRollSpeed);
	Ini.Set("Aircraft Settings", "Max Yaw Speed", AP.MaxYawSpeed);
	Ini.Set("Aircraft Settings", "Pitch Speed Multiplier", AP.PitchSpeedMultiplier);
	Ini.Set("Aircraft Settings", "Roll Speed Multiplier", AP.RollSpeedMultiplier);
	Ini.Set("Aircraft Settings", "Yaw Speed Multiplier", AP.YawSpeedMultiplier);

	Ini.Set("Storage", "Current Waypoint", AP.WaypointNum);
	Ini.Set("Storage", "Altitude Calibration", AP.Sealevel_Calibrate);
	Ini.Set("Storage", "Autopilot Enabled", AP.ApEngaged);
	Ini.Set("Storage", "Cruise Control Enabled", AP.CruiseEngaged);
	Ini.Set("Storage", "Cruise Saved Speed", AP.CruiseSpeed);
	Ini.Set("Storage", "Cruise Saved Alt", AP.CruiseAltitude);
}
if (WorkMode == "ils")
{
	Ini.Set("Airport Settings", "Use Sound Block", SB.ServiceEnabled);
	Ini.Set("Airport Settings", "Radio Channel", transponder.Channel);
	Ini.Set("Airport Settings", "ILS Channel", transponder.ILS_Channel);
	Ini.Set("Airport Settings", "Airport Forward Callsign", transponder.Callsign);
	Ini.Set("Airport Settings", "Airport Backward Callsign", transponder.Callsign2);
	Ini.Set("Airport Settings", "Runway Start Beacon Tag", ils.StartBeaconTag);
	Ini.Set("Airport Settings", "Runway Stop Beacon Tag", ils.StopBeaconTag);
}

Me.CustomData = Ini.ToString();
}
#endregion
#region Main
public void Main(string argument, UpdateType uType)
{
double lastRun = Runtime.TimeSinceLastRun.TotalSeconds;
timeForUpdateAP += lastRun;
timeForUpdateINI += lastRun;
timeForUpdateBlocks += lastRun;
timeForAlarmDelay += lastRun;
if (WorkMode == "plane") if (AP.ErrListNotEmpty()) timeToClearErrors += lastRun;
if (wasMainPartExecuted) { RP.saveRuntime(); wasMainPartExecuted = false; }

if (uType == UpdateType.Terminal || uType == UpdateType.Script || uType == UpdateType.Trigger || uType == UpdateType.IGC)
{
	if (WorkMode == "plane")
	{
		int n = 0;
		bool isNumeric = int.TryParse(argument, out n);
		if (isNumeric == true)
		{
			AP.WaypointNum = Math.Max(n, 0);
			Ini.Set("Storage", "Current Waypoint", AP.WaypointNum);
			Me.CustomData = Ini.ToString();
		}
		else
		{
			string[] parsedArg = argument.Split(' ');
			if (parsedArg.Length == 2)
				switch (parsedArg[0].ToLower())
				{
					case "land":
						DisAll();
						AP.OverrideLanding(parsedArg[1]);
						break;
					case "takeoff":
						DisAll();
						AP.OverrideTakeoff(parsedArg[1]);
						break;
					case "import":
						AP.ImportRoute(parsedArg[1]);
						break;
					case "export":
						AP.ExportRoute(parsedArg[1]);
						break;
					case "build":
						AP.BuildRoute(false, parsedArg[1]);
						break;
					case "build_circle":
						AP.BuildRoute(true, parsedArg[1]);
						break;
					default: break;
				}
			else if (parsedArg.Length == 3) switch (parsedArg[0].ToLower())
				{
					case "build":
						AP.BuildRoute(false, parsedArg[2], parsedArg[1]);
						break;
					case "build_circle":
						AP.BuildRoute(true, parsedArg[2], parsedArg[1]);
						break;
					case "land":
						DisAll();
						AP.OverrideLanding(parsedArg[1], parsedArg[2]);
						break;
					case "takeoff":
						DisAll();
						AP.OverrideTakeoff(parsedArg[1], parsedArg[2]);
						break;
					default: break;
				}
			else switch (argument.ToLower())
				{
					case "calibrate":
						AP.CalibrateAltitude();
						break;
					case "record_point":
						AP.RecordRoutePoint();
						break;
					case "repeat":
						AP.Repeat(!AP.RepeatRoute);
						break;
					case "repeat_on":
						AP.Repeat(true);
						break;
					case "repeat_off":
						AP.Repeat(false);
						break;
					case "autopilot":
						AP.SwitchCruise(false); AP.ResetBools();
						AP.SwitchAP(!AP.ApEngaged);
						break;
					case "ap_on":
						AP.SwitchCruise(false); AP.ResetBools();
						AP.SwitchAP(true);
						break;
					case "ap_off":
						AP.SwitchAP(false); AP.ResetBools();
						break;
					case "cruise":
						AP.SwitchAP(false); AP.ResetBools();
						AP.SwitchCruise(!AP.CruiseEngaged);
						break;
					case "cruise_on":
						AP.SwitchAP(false); AP.ResetBools();
						AP.SwitchCruise(true);
						break;
					case "cruise_off":
						AP.SwitchCruise(false); AP.ResetBools();
						break;
					case "brakes":
						LG.SetBrakes(!LG.Brakes);
						break;
					case "stop":
						AP.OverrideStop();
						break;
					case "land":
						DisAll();
						AP.OverrideLanding("");
						break;
					case "takeoff":
						DisAll();
						AP.OverrideTakeoff("");
						break;
					case "next": AP.ResetBools(); AP.WaypointNum++; break;
					case "prev": AP.ResetBools(); AP.WaypointNum--; break;
					case "recompile":
						Initialize(); break;
					default:
						break;
				}
		}
	}
	switch (argument.ToLower())
	{
		case "toggle":
			if (Runtime.UpdateFrequency == UpdateFrequency.Update1 || Runtime.UpdateFrequency == UpdateFrequency.Update10 || Runtime.UpdateFrequency == UpdateFrequency.Update100)
				Runtime.UpdateFrequency = UpdateFrequency.None;
			else Runtime.UpdateFrequency = UpdateFrequency.Update1; break;
		default: break;
	}
}
if (timeForAlarmDelay >= SB.CurDelay) { timeForAlarmDelay = 0; SB.update(); }
if (timeForUpdateINI >= iniUpd)
{
	timeForUpdateINI = 0;
	ParseIni();
	transponder.CheckChannels();
	if (WorkMode == "plane") AP.UpdateAngles();
}
if (timeForUpdateBlocks >= blocksUpd)
{
	timeForUpdateBlocks = 0;
	if (WorkMode == "plane") { AP.updateBlocks(RC_TAG, WB_TAG, Include_TAG); LG.updateBlocks(LG_TAG, LGIndicator_TAG); AP.ResetInput(); }
	if (WorkMode == "ils") ils.updateBlocks();
	SB.updateBlocks(SB_TAG);
	displayScheduler.updateBlocks();
	wasMainPartExecuted = true;
}
if (timeToClearErrors >= errUpd)
{
	timeToClearErrors = 0;
	if (WorkMode == "plane") AP.ClearErrorList();
}
if (timeForUpdateAP >= timeLimit)
{
	timeForUpdateAP = 0;
	if (WorkMode == "plane") { AP.Update(); LG.update(); }
	if (WorkMode == "ils") ils.update();
	RP.update();
	displayScheduler.ShowStatus();
	wasMainPartExecuted = true;
}
}
void DisAll() { AP.SwitchAP(false); AP.SwitchCruise(false); AP.ResetBools(); }
#endregion
#region Runtime
class RuntimeProfiler
{
Program Parent; double EMA_A = 0.003; public double lastMainPartRuntime = 0, RunTimeAvrEMA = 0;
public RuntimeProfiler(Program parent) { Parent = parent; }
public void update() { RunTimeAvrEMA = Math.Round(EMA_A * lastMainPartRuntime + (1 - EMA_A) * RunTimeAvrEMA, 4); }
public void saveRuntime() { lastMainPartRuntime = Parent.Runtime.LastRunTimeMs; }
}
#endregion
#region DisplayScheduler
class DisplayScheduler
{
StringBuilder MyScreenData;
Program Parent;
RuntimeProfiler RP;
string[] LCD_TAG;
string LGTag, LGIndTag, WorkMode, SBtag;
AutoPilot ap;
Transponder tp;
LandingGear LG;
SoundBlock SB;
IMyTextSurface MyScreen;
List<IMyTextSurface> TCASLCDs, NavLCDs, AvionicsLCDs, FlightsLCDs;
public DisplayScheduler(Program parent, RuntimeProfiler rt, string[] lcd_tag, string lgTag, string lgIndTag, string workmode, AutoPilot AP, Transponder Trans, LandingGear lg, SoundBlock sb, string sbtag)
{
	Parent = parent; RP = rt; LCD_TAG = lcd_tag; LGTag = lgTag; LGIndTag = lgIndTag; WorkMode = workmode; ap = AP; tp = Trans; LG = lg; SB = sb; SBtag = sbtag;
	TCASLCDs = new List<IMyTextSurface>(); NavLCDs = new List<IMyTextSurface>(); AvionicsLCDs = new List<IMyTextSurface>();
	updateBlocks(); MyScreenData = new StringBuilder();
}
public DisplayScheduler(Program parent, RuntimeProfiler rt, string[] lcd_tag, string workmode, Transponder Trans, SoundBlock sb, string sbtag)
{
	Parent = parent; RP = rt; LCD_TAG = lcd_tag; WorkMode = workmode; tp = Trans; SB = sb; SBtag = sbtag;
	FlightsLCDs = new List<IMyTextSurface>();
	updateBlocks(); MyScreenData = new StringBuilder();
}

public void updateBlocks()
{
	if (WorkMode == "plane")
	{
		TCASLCDs.Clear(); NavLCDs.Clear(); AvionicsLCDs.Clear();

		List<IMyTerminalBlock> lcdHosts = new List<IMyTerminalBlock>();
		Parent.GridTerminalSystem.GetBlocksOfType(lcdHosts, block => block as IMyTextSurfaceProvider != null && block.IsSameConstructAs(Parent.Me));
		List<string> lines = new List<string>();
		IMyTextSurface lcd;

		foreach (var block in lcdHosts)
		{
			if (block.CustomData.Contains(LCD_TAG[0]))
			{
				lines.Clear();
				new StringSegment(block.CustomData).GetLines(lines);
				foreach (var line in lines)
				{
					if (line.Contains(LCD_TAG[0]))
					{
						for (int i = 1; i < LCD_TAG.Length; i++)
						{
							if (line.Contains(LCD_TAG[i]))
							{
								if (block as IMyTextSurface != null)
									lcd = block as IMyTextSurface;
								else
								{
									int displayIndex = 0;

									int.TryParse(line.Replace(LCD_TAG[0] + LCD_TAG[i], ""), out displayIndex);

									IMyTextSurfaceProvider t_sp = block as IMyTextSurfaceProvider;
									displayIndex = Math.Max(0, Math.Min(displayIndex, t_sp.SurfaceCount));
									lcd = t_sp.GetSurface(displayIndex);
								}

								lcd.ContentType = ContentType.TEXT_AND_IMAGE;
								switch (i)
								{
									case 1:
										lcd.FontSize = 0.88f;
										lcd.Font = "Monospace";
										TCASLCDs.Add(lcd);
										break;
									case 2:
										lcd.FontSize = 1.1f;
										lcd.Font = "Monospace";
										NavLCDs.Add(lcd);
										break;
									case 3:
										lcd.FontSize = 1.1f;
										lcd.Font = "Monospace";
										AvionicsLCDs.Add(lcd);
										break;
									default:
										break;
								}
								break;
							}
						}
					}
				}
			}
		}
	}
	if (WorkMode == "ils")
	{
		FlightsLCDs.Clear();
		List<IMyTerminalBlock> lcdHosts = new List<IMyTerminalBlock>();
		Parent.GridTerminalSystem.GetBlocksOfType(lcdHosts, block => block as IMyTextSurface != null && block.IsSameConstructAs(Parent.Me));
		IMyTextSurface lcd;
		foreach (var block in lcdHosts)
		{
			if (block as IMyTextSurface != null && block.CustomData.Contains(LCD_TAG[4]))
			{
				lcd = block as IMyTextSurface;
				lcd.ContentType = ContentType.TEXT_AND_IMAGE;
				lcd.Font = "Monospace";
				FlightsLCDs.Add(lcd);
			}
		}
	}
	MyScreen = Parent.Me.GetSurface(0); MyScreen.ContentType = ContentType.TEXT_AND_IMAGE;
}
public void ShowStatus()
{
	MyScreenData.Clear(); MyScreenData.Append("       === PILOT ASISTANT SYSTEM ===");
	if (WorkMode == "plane")
	{
		if (!ap.RCReady) MyScreenData.Append("\n Critical Error: Ship Controller with\n '" + ap.RCName + "' name not found.\n");
		else if (ap.Sealevel_Calibrate == 0) MyScreenData.Append("\n Critical Error: Calibrate Sea Level!\n Use 'calibrate' arg.\n");
		else
		{
			bool AllDisplays = true;
			string errors = LCD_TAG[0] + " + ";

			if (TCASLCDs.Count > 0) foreach (var thisScreen in TCASLCDs) thisScreen.WriteText(tp.GetTCAS());
			else { errors += LCD_TAG[1] + ", "; AllDisplays = false; }

			if (NavLCDs.Count > 0) foreach (var thisScreen in NavLCDs) thisScreen.WriteText(ap.GetNav());
			else { errors += LCD_TAG[2] + ", "; AllDisplays = false; }

			if (AvionicsLCDs.Count > 0) foreach (var thisScreen in AvionicsLCDs) thisScreen.WriteText(ap.GetAvionics());
			else { errors += LCD_TAG[3] + ", "; AllDisplays = false; }

			errors = "\n LCD Error:\n" + errors + "displays were not found.\n";

			if (!AllDisplays) MyScreenData.Append(errors);
			if (LG.ServiceEnabled)
			{
				MyScreenData.Append("\n Landing Gear Service: ON");
				if (!LG.TReady) MyScreenData.Append("\n Landing Gear Error:\n '" + LGTag + "' Timer Block not found!");
				if (!LG.IReady) MyScreenData.Append("\n Landing Gear Error:\n '" + LGIndTag + "' Indicator Block not found\n (Any functional block. Add it to Timer's toolbar (on/off)).\n");
				if (!LG.CReady) MyScreenData.Append("\n Landing Gear Warn:\n '" + LGTag + "' Remote Control(s) not found\n (Used for braking. Place on subgrid suspensions).\n");
			}
			else MyScreenData.Append("\n Landing Gear Service: OFF");
			WriteSB();
			MyScreenData.Append("\n TCAS: ");
			if (ap.UseLandTCAS && !ap.UseTCAS) MyScreenData.Append("Only On Landing");
			else if (!ap.UseLandTCAS && ap.UseTCAS) MyScreenData.Append("Only In Flight");
			else if (ap.UseLandTCAS && ap.UseTCAS) MyScreenData.Append("ON");
			else if (!ap.UseLandTCAS && !ap.UseTCAS) MyScreenData.Append("OFF");
			MyScreenData.Append("\n GPWS: ");
			if (ap.UseLandGPWS && !ap.UseGPWS) MyScreenData.Append("Only On Landing");
			else if (!ap.UseLandGPWS && ap.UseGPWS) MyScreenData.Append("Only In Flight");
			else if (ap.UseLandGPWS && ap.UseGPWS) MyScreenData.Append("ON");
			else if (!ap.UseLandGPWS && !ap.UseGPWS) MyScreenData.Append("OFF");
			MyScreenData.Append(ap.GetErrorsList());
		}
	}
	else if (WorkMode == "ils")
	{
		if (FlightsLCDs.Count > 0) foreach (var thisScreen in FlightsLCDs) thisScreen.WriteText(tp.GetFlights());
		else MyScreenData.Append("\n LCD Error:\n '" + LCD_TAG[4] + "' displays were not found.\n");
		WriteSB();
	}
	MyScreenData.Append("\n Next Update: " + Math.Round(blocksUpd - Parent.timeForUpdateBlocks, 1) + " sec");

	MyScreenData.Append("\n Runtime: L " + RP.lastMainPartRuntime + " ms. Av " + RP.RunTimeAvrEMA + " ms\n Instructions used: " + Parent.Runtime.CurrentInstructionCount + "/" + Parent.Runtime.MaxInstructionCount + "\n");
	MyScreen.WriteText(MyScreenData); Parent.Echo(MyScreenData.ToString());
}
void WriteSB()
{
	if (SB.ServiceEnabled)
	{
		MyScreenData.Append("\n Warning System: ON");
		if (!SB.SReady) MyScreenData.Append("\n Warning System Error:\n '" + SBtag + "' Sound Block not found.\n");
		if (!SB.LReady) MyScreenData.Append("\n Warning System Error:\n '" + SBtag + "' Lighting Block not found.\n");
	}
	else MyScreenData.Append("\n Warning System: OFF");
}
}

#endregion
#region ILS
class ILS
{
public string StartBeaconTag = "RW_Start", StopBeaconTag = "RW_Stop";
bool Ready = false;
Transponder tp;
Program Parent;
List<IMyTerminalBlock> Blocks;
IMyTerminalBlock BeaconStart, BeaconStop;
public ILS(Program parent, Transponder trans)
{
	tp = trans; Parent = parent;
	Blocks = new List<IMyTerminalBlock>();
	updateBlocks();
}
public void update()
{
	if (!Ready) { Parent.Echo(StartBeaconTag + " or " + StopBeaconTag + " blocks not found."); return; }
	if (BeaconStart.Closed || BeaconStop.Closed) return;
	Vector3D Start = BeaconStart.GetPosition(); Vector3D Stop = BeaconStop.GetPosition();
	if (Start == Stop) { Parent.Echo(StartBeaconTag + " or " + StopBeaconTag + " blocks not found."); return; }
	tp.SendILS(Start, Stop); Parent.Echo("Sending ILS signal on: " + tp.ILS_Channel + " channel.\nWith: " + tp.Callsign + " (Forward) and " + tp.Callsign2 + " (Backward) callsigns.");
}
public bool updateBlocks()
{
	BeaconStart = null; BeaconStop = null;
	Blocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomData.Contains(StartBeaconTag) && x != Parent.Me);
	if (Blocks.Count > 0) BeaconStart = Blocks.First(x => x.CustomData.Contains(StartBeaconTag) && x != Parent.Me);
	Blocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomData.Contains(StopBeaconTag) && x != Parent.Me);
	if (Blocks.Count > 0) BeaconStop = Blocks.First(x => x.CustomData.Contains(StopBeaconTag) && x != Parent.Me);
	if (BeaconStart != null && BeaconStop != null) Ready = true; else Ready = false;
	return false;
}
}
#endregion
#region Transponder
class Transponder
{
IMyIntergridCommunicationSystem IGC;
IMyBroadcastListener Reciever, ILSlistener;
public string Channel = "Default", ILS_Channel = "Default_ILS", Callsign = "Default", Callsign2 = "Default", LastILS = "";
string LCnl = "Default", LILSCnl = "Default_ILS";
public bool ILSChosen = false; bool LastClearToLand = false; int LastAltPlus = 0, LastNumFlights = 0;
StringBuilder SBFlightsList, SBTCAS;
SoundBlock SB;
public int Priority = 0;
public double MaxDistToILSRunway = 10000;
public MyIGCMessage Message, ILScords;

public Transponder(IMyIntergridCommunicationSystem igc, string workmode, SoundBlock sb)
{
	IGC = igc; SB = sb;
	if (workmode == "plane")
	{
		Message = new MyIGCMessage();
		ILScords = new MyIGCMessage();
		Reciever = IGC.RegisterBroadcastListener(Channel);
		ILSlistener = IGC.RegisterBroadcastListener(ILS_Channel);
	}
	if (workmode == "ils")
	{
		Message = new MyIGCMessage();
		Reciever = IGC.RegisterBroadcastListener(Channel);
	}
	SBFlightsList = new StringBuilder();
	SBTCAS = new StringBuilder();
	ClearTCAS("");
}
public void CheckChannels()
{
	if (Channel != LCnl) { IGC.DisableBroadcastListener(Reciever); Reciever = IGC.RegisterBroadcastListener(Channel); LCnl = Channel; }
	if (ILSlistener != null && ILS_Channel != LILSCnl) { IGC.DisableBroadcastListener(ILSlistener); ILSlistener = IGC.RegisterBroadcastListener(ILS_Channel); LILSCnl = ILS_Channel; }
}
public bool CheckILS(string expectedCallsign, Vector3D MyPos, out Vector3D RStart, out Vector3D RStop, Vector3D MyHeading)
{
	RStart = new Vector3D(0, 0, 0);
	RStop = new Vector3D(0, 0, 0);
	double minDistance = MaxDistToILSRunway;

	while (ILSlistener.HasPendingMessage)
	{
		ILScords = ILSlistener.AcceptMessage();
		string receivedData = ILScords.Data.ToString();
		string[] parsingRecieved = receivedData.Split('|');
		if (parsingRecieved.Length == 3)
		{
			if (expectedCallsign != "default" && expectedCallsign != parsingRecieved[0]) continue;

			if (ILSChosen && parsingRecieved[0] != LastILS) continue;
			Vector3D CurrentRStart, CurrentRStop;
			Vector3D.TryParse(parsingRecieved[1], out CurrentRStart);
			Vector3D.TryParse(parsingRecieved[2], out CurrentRStop);

			double distance = Vector3D.Distance(MyPos, CurrentRStart), angle = 0;
			if (MyHeading != Vector3D.Zero) angle = Vector3D.Angle(CurrentRStop - CurrentRStart, MyHeading);
			if (distance < minDistance && angle < 1.571)
			{
				LastILS = parsingRecieved[0];
				minDistance = distance;
				RStart = CurrentRStart;
				RStop = CurrentRStop;
			}

		}
	}
	if (LastILS != "") ILSChosen = true;
	return false;
}
public void ClearLastILS() { LastILS = ""; ILSChosen = false; }
public bool CheckLandingPriority(int myDistance, Vector3D myPoint)
{
	IGC.SendBroadcastMessage<string>(Channel, Callsign + "|" + Priority + "|" + myDistance + "|" + myPoint.ToString(), TransmissionDistance.AntennaRelay);
	ClearTCAS("land");
	SBTCAS.Append(" " + Callsign.PadRight(9).Substring(0, 9) + "| " + Priority.ToString().PadRight(6).Substring(0, 6) + "| " + myDistance + " m\n");
	bool clearToLand = true;
	if (Reciever.HasPendingMessage)
	{
		while (Reciever.HasPendingMessage)
		{
			Message = Reciever.AcceptMessage();

			string receivedData = Message.Data.ToString();

			string[] parsingRecieved = receivedData.Split('|');
			if (parsingRecieved.Length != 4) continue;

			Vector3D otherPoint = Vector3D.Zero;
			Vector3D.TryParse(parsingRecieved[3], out otherPoint);
			if (otherPoint == Vector3D.Zero) continue;
			if (Vector3D.Distance(myPoint, otherPoint) < 30)
			{
				SBTCAS.Append(" " + parsingRecieved[0].ToUpper().PadRight(9).Substring(0, 9) + "| " + parsingRecieved[1].PadRight(6).Substring(0, 6) + "| " + parsingRecieved[2] + " m\n");
				int receivedPriority = Convert.ToInt32(parsingRecieved[1]);
				int receivedDistance = Convert.ToInt32(parsingRecieved[2]);
				if (Priority < receivedPriority)
				{
					if (myDistance < receivedDistance) clearToLand = true;
					else if (receivedDistance < 250) clearToLand = false;
					else clearToLand = true;
				}

				else if (Priority == receivedPriority)
				{
					if (myDistance < receivedDistance) clearToLand = true;
					else if (myDistance > receivedDistance) clearToLand = false;
				}

				else if (Priority > receivedPriority)
				{
					if (myDistance > receivedDistance) clearToLand = false;
					else if (myDistance < 250) clearToLand = true;
					else clearToLand = false;
				}
			}
		}
	}
	if (clearToLand) SBTCAS.Append("\n LAND DECISION: [CONTINUE] \n");
	else SBTCAS.Append("\n LAND DECISION: [GO-AROUND] \n");
	if (clearToLand != LastClearToLand) { LastClearToLand = clearToLand; if (clearToLand) SB.Request("Yes"); else SB.Request("No"); }
	return clearToLand;
}
public int CheckTCAS(bool show, Vector3D myPos, int myHeading, int myAltitude, int mySpeed)
{
	int myAltPlus = 0;
	IGC.SendBroadcastMessage<string>(Channel, Callsign + "|" + Priority + "|" + myPos.ToString() + "|" + myHeading + "|" + myAltitude + "|" + mySpeed, TransmissionDistance.AntennaRelay);
	if (show)
	{
		ClearTCAS("flight");
		SBTCAS.Append(" " + Callsign.PadRight(9).Substring(0, 9) + "| " + Priority.ToString().PadRight(6).Substring(0, 6) + "| " + myAltitude + " m\n");
	}
	if (!Reciever.HasPendingMessage) { SBTCAS.Append("\n TCAS DECISION: [STAY]"); return myAltPlus; }

	while (Reciever.HasPendingMessage)
	{

		Message = Reciever.AcceptMessage();

		string receivedData = Message.Data.ToString();

		string[] parsingRecieved = receivedData.Split('|');
		if (parsingRecieved.Length != 6) continue;

		Vector3D otherPos;
		Vector3D.TryParse(parsingRecieved[2], out otherPos);

		double distanceBetween = Vector3D.Distance(myPos, otherPos);

		if (distanceBetween > 5000) continue;

		SBTCAS.Append(" " + parsingRecieved[0].ToUpper().PadRight(9).Substring(0, 9) + "| " + parsingRecieved[1].PadRight(6).Substring(0, 6) + "| " + parsingRecieved[4] + " m\n");

		int otherAltitude = Convert.ToInt32(parsingRecieved[4]);
		if (Math.Abs(myAltitude + myAltPlus - otherAltitude) > 200) continue;

		int otherPriority = Convert.ToInt32(parsingRecieved[1]);

		if (Priority < otherPriority) continue;

		else if (Priority > otherPriority) myAltPlus += 150;

		else if (Priority == otherPriority)
		{
			int otherHeading = Convert.ToInt32(parsingRecieved[3]);
			if (myHeading < otherHeading) continue;
			else if (myHeading > otherHeading) myAltPlus += 150;
			else
			{
				int otherSpeed = Convert.ToInt32(parsingRecieved[5]);
				if (mySpeed < otherSpeed) continue;
				else if (mySpeed > otherSpeed) myAltPlus += 150;
			}
		}
	}
	if (myAltPlus > 0) SBTCAS.Append("\n TCAS DECISION: [+" + myAltPlus + "m]");
	else SBTCAS.Append("\n TCAS DECISION: [STAY]");
	if (myAltPlus != LastAltPlus) { if (myAltPlus < LastAltPlus) SB.Request("Yes"); else SB.Request("No"); LastAltPlus = myAltPlus; }
	return myAltPlus;
}
public void ClearTCAS(string mode)
{
	SBTCAS.Clear();
	SBTCAS.Append(" === TCAS PRIORITY LIST ===\n\n");
	switch (mode)
	{
		case "land":
			SBTCAS.Append("     MODE: [LANDING]\n\n CALLSIGN | PRIOR | DISTANCE\n");
			break;
		case "flight":
			SBTCAS.Append("     MODE: [FLIGHT]\n\n CALLSIGN | PRIOR | ALTITUDE\n");
			break;
		default:
			SBTCAS.Append("     MODE: [STANDBY]\n\n -------- | ----- | --------\n");
			break;
	}
}
public string GetTCAS()
{
	return SBTCAS.ToString();
}
public void SendILS(Vector3D Start, Vector3D Stop)
{
	IGC.SendBroadcastMessage(ILS_Channel, Callsign + "|" + Start.ToString() + "|" + Stop.ToString(), TransmissionDistance.AntennaRelay);
	IGC.SendBroadcastMessage(ILS_Channel, Callsign2 + "|" + Stop.ToString() + "|" + Start.ToString(), TransmissionDistance.AntennaRelay);
	SBFlightsList.Clear();
	SBFlightsList.Append("               === VISIBLE FLIGHTS LIST ===\n\n CALLSIGN | PRIOR | DISTANCE | HEADING | ALTITUDE | SPEED\n");
	int numFlights = 0;
	while (Reciever.HasPendingMessage)
	{
		Message = Reciever.AcceptMessage();
		string receivedData = Message.Data.ToString();
		string[] parsingRecieved = receivedData.Split('|');
		if (parsingRecieved.Length == 6)
		{
			numFlights++;
			Vector3D otherPos;
			Vector3D.TryParse(parsingRecieved[2], out otherPos);
			int dist = (int)Vector3D.Distance(Start, otherPos);
			SBFlightsList.Append(" " + parsingRecieved[0].ToUpper().PadRight(9).Substring(0, 9) +
				"| " + parsingRecieved[1].PadRight(6).Substring(0, 6) +
				"| " + (dist.ToString() + " m").PadRight(9).Substring(0, 9) +
				"| " + (parsingRecieved[3] + "°").PadRight(8).Substring(0, 8) +
				"| " + (parsingRecieved[4] + " m").PadRight(9).Substring(0, 9) +
				"| " + (parsingRecieved[5] + " m/s").PadRight(8).Substring(0, 8) + "\n");
		}
		else if (parsingRecieved.Length == 4)
		{
			numFlights++;
			SBFlightsList.Append(" " + parsingRecieved[0].ToUpper().PadRight(9).Substring(0, 9) +
				"| " + parsingRecieved[1].PadRight(6).Substring(0, 6) +
				"| " + ("L " + parsingRecieved[2] + " m").PadRight(9).Substring(0, 9) +
				"|         |          |         \n");
		}
	}
	if (LastNumFlights != numFlights) { if (numFlights < LastNumFlights) SB.Request("No"); else SB.Request("Yes"); LastNumFlights = numFlights; }
}
public string GetFlights()
{
	return SBFlightsList.ToString();
}
}
#endregion
#region Landing Gear
class LandingGear
{
public bool GearStatus = true, Ready = false, CReady = false, TReady = false, IReady = false, Brakes = false, ServiceEnabled = true;
Program Parent; SoundBlock SB;
IMyTimerBlock lgTimerBlock;
IMyFunctionalBlock lgIndicatorBlock;
List<IMyTerminalBlock> Blocks;
List<IMyShipController> controllers;
public LandingGear(Program parent, string lgTag, string lgIndicatorName, SoundBlock sb) { Parent = parent; SB = sb; controllers = new List<IMyShipController>(); Blocks = new List<IMyTerminalBlock>(); updateBlocks(lgTag, lgIndicatorName); }
public void GearUp() { if (!Ready) return; update(); if (GearStatus) { lgTimerBlock?.Trigger(); GearStatus = false; SB.Request("GearUp"); } }
public void GearDown() { if (!Ready) return; update(); if (!GearStatus) { lgTimerBlock?.Trigger(); GearStatus = true; SB.Request("GearDown"); } }
public void update() { if (!Ready) return; GearStatus = lgIndicatorBlock.Enabled; }
public void SetBrakes(bool cond) { if (CReady) { Brakes = CheckHB(cond); if (Brakes == cond) return; foreach (var block in controllers) block.HandBrake = cond; Brakes = cond; if (cond) SB.Request("BrakesOn"); else SB.Request("BrakesOff"); } }
bool CheckHB(bool c) { foreach (var b in controllers) if (c ? !b.HandBrake : b.HandBrake) return !c; return c; }
public void updateBlocks(string lgTag, string lgIndicatorName)
{
	if (!ServiceEnabled) { Ready = false; CReady = false; return; }
	controllers.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(controllers, x => x is IMyRemoteControl && x.CustomName.Contains(lgTag));
	if (controllers.Count > 0) CReady = true; else CReady = false;
	Blocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomName.Contains(lgIndicatorName));
	if (Blocks.Count > 0) lgIndicatorBlock = Blocks.First() as IMyFunctionalBlock;
	Blocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x is IMyTimerBlock && x.CustomName.Contains(lgTag));
	if (Blocks.Count > 0) lgTimerBlock = Blocks.First() as IMyTimerBlock;
	TReady = lgTimerBlock != null; IReady = lgIndicatorBlock != null;
	if (lgTimerBlock != null && lgIndicatorBlock != null) Ready = true; else Ready = false;
}
}
#endregion
#region SoundBlock
class SoundBlock
{
public bool SReady = false, LReady = false, ServiceEnabled = true;
Program Parent;
List<IMySoundBlock> SBlocks;
List<IMyLightingBlock> LBlocks;
List<Sound> Queue; int CurTimes = 0; public double CurDelay = 0;
public SoundBlock(Program parent, string sb_tag) { Parent = parent; LBlocks = new List<IMyLightingBlock>(); SBlocks = new List<IMySoundBlock>(); Queue = new List<Sound>(); updateBlocks(sb_tag); }
public void Request(string action)
{
	if (!SReady && !LReady) return;
	switch (action)
	{
		case "GearUp": AddAction("SoundBlockAlert1", Color.Yellow, 1, 0.10, 0.15); AddAction("SoundBlockAlert2", Color.RoyalBlue, 1, 0.20, 0.15); break;
		case "GearDown": AddAction("SoundBlockAlert1", Color.Yellow, 1, 0.10, 0.15); AddAction("SoundBlockAlert2", Color.SeaGreen, 1, 0.20, 0.15); break;
		case "BrakesOn": AddAction("SoundBlockAlert2", Color.LightGoldenrodYellow, 1, 0.10, 0.05); AddAction("SoundBlockAlert1", Color.Orange, 1, 0.10, 0.05); break;
		case "BrakesOff": AddAction("SoundBlockAlert1", Color.LightGoldenrodYellow, 1, 0.10, 0.05); AddAction("SoundBlockAlert2", Color.ForestGreen, 1, 0.10, 0.05); break;
		case "SpaceInput": AddAction("MusFun", Color.Gold, 3, 0.10, 0.085); break;
		case "AutoStop": AddAction("SoundBlockAlert1", Color.Red, 4, 0.10, 0.15); break;
		case "AutoStart": AddAction("SoundBlockAlert2", Color.Green, 2, 0.10, 0.15); break;
		case "ObjComp": AddAction("SoundBlockObjectiveComplete", Color.LimeGreen, 1, 2, 2); break;
		case "Yes": AddAction("SoundBlockAlert2", Color.SpringGreen, 10, 0.05, 0.05); break;
		case "No": AddAction("SoundBlockAlert1", Color.OrangeRed, 10, 0.05, 0.05); break;
		case "GPWS": AddAction("MusComp_08", Color.Red, 8, 0.1, 0.1); break;
		default: break;
	}
}
void AddAction(string sound, Color col, int times, double playTime, double delay) { Sound element = new Sound() { Color = col, Delay = delay, PlayTime = playTime, SoundName = sound, Times = times }; Queue.Add(element); Sound element2 = new Sound() { Color = Color.Black, Delay = 0.1, PlayTime = 0.1, SoundName = "none", Times = 1 }; Queue.Add(element2); }
public void update()
{
	if ((!SReady && !LReady) || Queue.Count == 0) return;
	if (Play(Queue.First())) Queue.RemoveAt(0);
}
bool Play(Sound element)
{
	if (CurTimes >= element.Times) { CurTimes = 0; CurDelay = 0.1; return true; }
	CurDelay = element.Delay;
	if (SReady) foreach (var S in SBlocks)
		{
			S.Stop();
			if (element.SoundName != "none")
			{
				S.SelectedSound = element.SoundName;
				S.LoopPeriod = (float)element.PlayTime;
				S.Play();
			}
		}
	if (LReady) foreach (var L in LBlocks)
		{
			L.Color = element.Color;
			if (element.SoundName != "none")
				L.BlinkIntervalSeconds = (float)element.PlayTime * 2;
			else L.BlinkIntervalSeconds = 0;
		}
	CurTimes++;
	return false;
}
public void updateBlocks(string sb_tag)
{
	if (!ServiceEnabled) { SReady = LReady = false; return; }
	SBlocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(SBlocks, x => x.CustomName.Contains(sb_tag));
	SReady = SBlocks.Count() > 0;
	LBlocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(LBlocks, x => x.CustomName.Contains(sb_tag));
	LReady = LBlocks.Count() > 0;
	if (LReady) foreach (var L in LBlocks) L.BlinkLength = 50;
}
class Sound
{
	public string SoundName { get; set; }
	public int Times { get; set; }
	public double PlayTime { get; set; }
	public double Delay { get; set; }
	public Color Color { get; set; }
}
}
#endregion
class AutoPilot
{
#region APGlobal
Program Parent;
StringBuilder SBAvionics, SBNav, SBErrors;

List<RoutePoint> CurrentRoute, CruiseRoute;
Vector3D PointToPointCourse, SlidingTarget, DirToSlidingTarget, MyPos, MyVel, VecToRunwayStart, GravityVector, RotationAxis;
string goAroundType = "none", overrideType = "none", CurrentRouteName = "";

double DistanceToPoint, SlidingTargetDist, MyVelHor, RunwayRelativeVel = 0, MyVelVert, PitchCorrection, MyAltitude = 0, Heading = 0, FullDistance = 0, SavedDistance = 0, SavedDistance2 = 0, time_for_wait = 0;
Vector3D PrevRunwayStart = new Vector3D(0, 0, 0), SavedRStart = new Vector3D(0, 0, 0), SavedRStop = new Vector3D(0, 0, 0);

Vector3D LastPointTo = new Vector3D(-10, -10, -10), LastPointFrom = new Vector3D(10, 10, 10);
double LastAltitude = 1000, LastSpeed = 100, GearUpAlt = 25, TakeoffEndAlt = 100, ShowSpeed = 0, ShowAlt = 0, LastSurfaceAlt = 0, SurfaceAlt = 0, AltLimit = 0, AltAboveRW = 0;
bool switchBackupPoints = true, rejectILSTakeoff = false, onGlideSlope, PullUp = false, lastRC = false;
Vector3D planetCenter, planetDelta; double planetRadius = 0, planetAngleDeg = 0, RunwayAlt = 0;
public double VRotate = 80, V2 = 100, MinLandVelocity = 60, MaxLandVelocity = 80, AirbrakeVelocity = 90, BasicLandingAngle = 3, Sealevel_Calibrate = 0;
public double MaxPitchAngle = 45, MaxRollAngle = 45, PitchSpeedMultiplier = 1, YawSpeedMultiplier = 1, RollSpeedMultiplier = 1, MaxPitchSpeed = 15, MaxRollSpeed = 15, MaxYawSpeed = 5;
double MaxRadPitch = 0, MaxRadRoll = 0, MaxRadPitchSpd = 0, MaxRadRollSpd = 0, MaxRadYawSpd = 0;

public int BasicAPSpeed = 100, BasicAPAltitude = 2500, BasicWaitTime = 60; //m/s, m, sec
public string BasicILSCallsign = "default", AutopilotTimerName = "default"; string ReqCallsign = "default";
public double CruiseSpeed = 0, CruiseAltitude = 0;

float ThrustPerc = 0;

public string string_status = "None", RCName = "", WBName = "";
public int WaypointNum = 9999, CruiseWNum = 9999, lastWaypointNum = 0, TickCounter = 0;
public bool RepeatRoute = true, ApEngaged = false, CruiseEngaged = false, UseTCAS = true, UseLandTCAS = true, UseGPWS = true, UseLandGPWS = true, UseDamp = true, AlwaysDampiners = false, RCReady;

MyIni Ini;
IMyGridProgramRuntimeInfo Runtime;
IMyProgrammableBlock Me;
IMyShipController UnderControl; Vector3 MoveInput = Vector3.Zero; Vector2 RotationInput = Vector2.Zero; int ClickCounter = 0; bool wasSpaceC = false, wasWS = false, wasUpDown = false, wasLeftRight = false;
IMyShipController RC;
IMyTerminalBlock WaypointsBlock;
List<IMyShipController> controllers;
List<IMyGyro> gyros;
List<IMyThrust> thrusters;
List<IMyDoor> airbrakes;
List<double> VelBuff;
PID pitchPID, rollPID, yawPID, VertVelPD, SurfVelPD;


Transponder tp;
LandingGear LG;
SoundBlock SB;

public AutoPilot(Program parent, string controllerName, string waypointsBlockName, string IncludeTag, IMyProgrammableBlock me, MyIni ini, IMyGridProgramRuntimeInfo runtime, Transponder Trans, LandingGear lg, SoundBlock sb)
{
	Parent = parent; Me = me; Ini = ini; Runtime = runtime; tp = Trans; LG = lg; SB = sb;
	RCName = controllerName; WBName = waypointsBlockName;
	pitchPID = new PID(2, 0, .5, -10, 10, timeLimit);
	rollPID = new PID(2, 0, .5, -10, 10, timeLimit);
	yawPID = new PID(2, 1, .5, -10, 10, timeLimit);
	VertVelPD = new PID(1, 0, .5, -10, 10, timeLimit);
	SurfVelPD = new PID(1, 0, 1, -10, 10, timeLimit);
	SBAvionics = new StringBuilder(); SBNav = new StringBuilder(); SBErrors = new StringBuilder();
	controllers = new List<IMyShipController>(); gyros = new List<IMyGyro>(); thrusters = new List<IMyThrust>(); airbrakes = new List<IMyDoor>();
	VelBuff = new List<double>(5);
	updateBlocks(controllerName, waypointsBlockName, IncludeTag);

	CurrentRoute = new List<RoutePoint>(); CruiseRoute = new List<RoutePoint>();

	if (RCReady)
	{
		ParseRoute(Parent.Storage, out CurrentRouteName, out CurrentRoute);
		CalculateFullDistance();
	}
	ReleaseControls();
}
#endregion

#region update
public void updateBlocks(string controllerName, string waypointsBlockName, string IncludeTag)
{
	gyros.Clear(); controllers.Clear(); thrusters.Clear(); airbrakes.Clear(); TickCounter = 0;
	Parent.GridTerminalSystem.GetBlocksOfType(controllers, x => x.CustomName.Contains(controllerName));
	if (controllers.Count > 0) { RC = controllers.First(); if (RC != null) RCReady = true; } else { RCReady = lastRC = false; return; }
	controllers.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(controllers, x => x.WorldMatrix.Up == RC.WorldMatrix.Up && x.CubeGrid == RC.CubeGrid);
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>(); Blocks.Clear();
	Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomName.Contains(waypointsBlockName));
	if (Blocks.Count > 0) WaypointsBlock = Blocks.First(); Blocks.Clear();
	Parent.GridTerminalSystem.GetBlocksOfType(gyros, x => x.IsSameConstructAs(RC));
	Parent.GridTerminalSystem.GetBlocksOfType(thrusters, x => x.IsSameConstructAs(RC) && (x.WorldMatrix.Backward == RC.WorldMatrix.Forward || x.CustomName.Contains(IncludeTag)));
	Parent.GridTerminalSystem.GetBlocksOfType(airbrakes, x => x.IsSameConstructAs(RC) && x.CustomName.Contains("Air Brake"));
	if (RCReady != lastRC) { GetPlanetStats(); lastRC = RCReady; }
}
public void UpdateAngles() { MaxRadPitch = MathHelper.ToRadians(MaxPitchAngle); MaxRadRoll = MathHelper.ToRadians(MaxRollAngle); MaxRadPitchSpd = MathHelper.ToRadians(MaxPitchSpeed); MaxRadRollSpd = MathHelper.ToRadians(MaxRollSpeed); MaxRadYawSpd = MathHelper.ToRadians(MaxYawSpeed); }
public bool Update()
{
	if (!RCReady || RC.Closed || Sealevel_Calibrate == 0) return false;
	TickCounter++;
	GravityVector = Vector3D.Normalize(RC.GetNaturalGravity());
	if (double.IsNaN(GravityVector.LengthSquared())) return false;
	GetAltitude();

	MyPos = RC.GetPosition();
	MyVel = RC.GetShipVelocities().LinearVelocity;
	MyVelVert = Math.Round(-MyVel.Dot(GravityVector), 2);
	MyVelHor = Math.Round(Vector3D.Reject(MyVel, GravityVector).Length(), 2);
	CalculateHeading();
	if (ApEngaged || CruiseEngaged || overrideType != "none")
	{
		UnderControl = GetControlledShipController();
		MoveInput = UnderControl.MoveIndicator;
		RotationInput = UnderControl.RotationIndicator;
		if (MoveInput.Y != 0 && !wasSpaceC) { ClickCounter++; wasSpaceC = true; }
		else if (MoveInput.Y == 0 && wasSpaceC) wasSpaceC = false;
		if (ClickCounter > 1) { ResetInput(); wasSpaceC = false; SB.Request("SpaceInput"); OverrideStop(); }
	}
	if (ApEngaged) updateCurrentPoint();
	if (CruiseEngaged) updateCruisePoint();
	if (!ApEngaged && !CruiseEngaged)
		switch (overrideType)
		{
			case "takeoff":
				ShowSpeed = VRotate; ShowAlt = TakeoffEndAlt + RunwayAlt;
				if (Takeoff(true, ReqCallsign, new Vector3D(0, 0, 0), new Vector3D(0, 0, 0))) EndOverride(true);
				break;
			case "landing":
				ShowSpeed = MaxLandVelocity; ShowAlt = RunwayAlt;
				if (Landing(true, ReqCallsign, new Vector3D(0, 0, 0), new Vector3D(0, 0, 0), BasicLandingAngle)) EndOverride(true);
				break;
			case "takeoff_cords":
				ShowSpeed = VRotate; ShowAlt = TakeoffEndAlt + RunwayAlt;
				if (Takeoff(false, ReqCallsign, SavedRStart, SavedRStop)) EndOverride(true);
				break;
			case "landing_cords":
				ShowSpeed = MaxLandVelocity; ShowAlt = RunwayAlt;
				if (Landing(false, ReqCallsign, SavedRStart, SavedRStop, BasicLandingAngle)) EndOverride(true);
				break;
			case "stop":
				ShowSpeed = 0; ShowAlt = 0; EndOverride(false);
				break;
			default: break;
		}
	updateAvionicsList();
	updateNavList();
	SaveWNum();
	return false;
}
public void OverrideStop()
{
	overrideType = "stop"; SwitchAP(false); SwitchCruise(false); LG.SetBrakes(false);
}
void EndOverride(bool sound)
{
	ReleaseControls(); overrideType = "none"; ReqCallsign = BasicILSCallsign;
	SavedRStart = SavedRStop = new Vector3D(0, 0, 0);
	if (sound) SB.Request("ObjComp"); ResetBools();
}
#endregion
#region route switch
void updateCurrentPoint()
{
	if (CurrentRoute.Count == 0) { SBErrors.Append("\nCurrent Route is empty!"); SwitchAP(false); return; }
	if (WaypointNum > CurrentRoute.Count - 1) { if (RepeatRoute) WaypointNum = 0; else { SwitchAP(false); return; } }
	switch (CurrentRoute[WaypointNum].OperationType)
	{
		case "Takeoff":
			ShowSpeed = VRotate; ShowAlt = 100;
			if (Takeoff(CurrentRoute[WaypointNum].ILS_TakeoffLanding, CurrentRoute[WaypointNum].ExpectedCallsign, CurrentRoute[WaypointNum].PointFrom, CurrentRoute[WaypointNum].PointTo))
			{ TriggerRouteTimer(CurrentRoute[WaypointNum].TimerName); WaypointNum++; }
			break;
		case "Landing":
			ShowSpeed = MaxLandVelocity; ShowAlt = 0;
			double tempAngle;
			if (CurrentRoute[WaypointNum].LandingAngle <= 0) tempAngle = BasicLandingAngle;
			else tempAngle = CurrentRoute[WaypointNum].LandingAngle;
			if (Landing(CurrentRoute[WaypointNum].ILS_TakeoffLanding, CurrentRoute[WaypointNum].ExpectedCallsign, CurrentRoute[WaypointNum].PointFrom, CurrentRoute[WaypointNum].PointTo, tempAngle))
			{ TriggerRouteTimer(CurrentRoute[WaypointNum].TimerName); WaypointNum++; }
			break;
		case "Wait":
			ShowSpeed = 0; ShowAlt = 0;
			double tempTime;
			if (CurrentRoute[WaypointNum].Time <= 0) tempTime = BasicWaitTime;
			else tempTime = CurrentRoute[WaypointNum].Time;
			if (Wait(tempTime))
			{ TriggerRouteTimer(CurrentRoute[WaypointNum].TimerName); WaypointNum++; }
			break;
		case "GoToPoint":
			double tempSpeed, tempAlt;
			if (CurrentRoute[WaypointNum].Speed <= 0) tempSpeed = BasicAPSpeed;
			else tempSpeed = CurrentRoute[WaypointNum].Speed;
			if (CurrentRoute[WaypointNum].Altitude == 0) tempAlt = BasicAPAltitude;
			else tempAlt = CurrentRoute[WaypointNum].Altitude;
			ShowSpeed = tempSpeed; ShowAlt = tempAlt;
			if (GoToPoint(false, CurrentRoute[WaypointNum].PointFrom, CurrentRoute[WaypointNum].PointTo, tempAlt, tempSpeed, true, true))
			{ TriggerRouteTimer(CurrentRoute[WaypointNum].TimerName); WaypointNum++; }
			break;
		default:
			ShowSpeed = 0; ShowAlt = 0;
			ReleaseControls();
			break;
	}
}
void TriggerRouteTimer(string blockname)
{
	if (blockname == "default") return;
	List<IMyTimerBlock> Blocks = new List<IMyTimerBlock>();
	Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomName.Contains(blockname));
	IMyTimerBlock Block;
	if (Blocks.Count > 0) Block = Blocks.First(); else { SBErrors.Append("\n" + blockname + " Not Found!"); return; }
	Block.Trigger();
}
void updateCruisePoint()
{
	if (MoveInput.Z != 0) { wasWS = true; SetThrustP(0); }
	else if (MoveInput.Z == 0 && wasWS) { RecordCruiseSpd(); wasWS = false; }
	if (RotationInput.X != 0) { wasUpDown = true; GyroOverride(false); }
	else if (RotationInput.X == 0 && wasUpDown) { RecordCruiseAlt(); wasUpDown = false; }
	if (RotationInput.Y != 0) { wasLeftRight = true; GyroOverride(false); }
	else if (RotationInput.Y == 0 && wasLeftRight) { CruiseRoute.Clear(); CruiseWNum = 0; wasLeftRight = false; }
	bool Rotate = wasLeftRight || wasUpDown;
	if (CruiseRoute.Count == 0) { CruiseWNum = 0; GenerateRoute(true, MyPos, MyPos + Vector3D.Reject(RC.WorldMatrix.Forward, GravityVector) * 1000, out CruiseRoute, CruiseAltitude, planetAngleDeg); }
	if (CruiseWNum > CruiseRoute.Count - 1) { CruiseWNum = 0; }
	switch (CruiseRoute[CruiseWNum].OperationType)
	{
		case "GoToPoint":
			if (GoToPoint(false, CruiseRoute[CruiseWNum].PointFrom, CruiseRoute[CruiseWNum].PointTo, CruiseAltitude, CruiseSpeed, !wasWS, !Rotate))
				CruiseWNum++;
			break;
		default:
			ReleaseControls();
			break;
	}
}
#endregion

#region Avionics list
void updateAvionicsList()
{
	SBAvionics.Clear();
	SBAvionics.Append("    === AVIONICS ===\n");
	SBAvionics.Append(" ALTITUDE   VELOCITY\n");
	SBAvionics.Append((" [" + Math.Round(MyAltitude, 1) + "m]").PadRight(12) + "[" + MyVelHor + "m/s]\n");
	SBAvionics.Append(" VERTICAL   RELATIVE\n");
	SBAvionics.Append((" [" + MyVelVert + "m/s]").PadRight(12) + "[" + RunwayRelativeVel + "m/s]\n");
	SBAvionics.Append(" THROTTLE   GEAR\n");
	SBAvionics.Append((" [" + Math.Round(ThrustPerc * 100, 1) + "%]").PadRight(12));
	if (LG.Ready) { if (LG.GearStatus) SBAvionics.Append("[DEPLOYED]\n"); else SBAvionics.Append("[RETRACTED]\n"); } else SBAvionics.Append("[NOT SET]\n");
	SBAvionics.Append(" HEADING    BRAKES\n" + (" [" + Heading + "°]").PadRight(12));
	if (LG.CReady) { if (LG.Brakes) SBAvionics.Append("[ENGAGED]\n"); else SBAvionics.Append("[RELEASED]\n"); } else SBAvionics.Append("[NOT SET]\n");
}

public string GetAvionics()
{
	return SBAvionics.ToString();
}
#endregion
#region Navigation list
void updateNavList()
{
	SBNav.Clear();
	SBNav.Append("   === NAVIGATION ===\n");

	if (ApEngaged)
	{
		SBNav.Append(" AUTOPILOT [ON]\n");
		SBNav.Append(" CUR. ROUTE [" + CurrentRouteName + "]\n");
		SBNav.Append(" ST. [" + string_status + "] [" + WaypointNum + "]\n");
		SBNav.Append(" DIST [" + Math.Round(CalculateDistancePassed() / 1000, 1) + "/" + Math.Round(FullDistance / 1000, 1) + "km]\n");
		SBNav.Append(" TO GO [" + Math.Round(CalculateDistanceLeft() / 1000, 1) + ":" + Math.Round(DistanceToPoint / 1000, 1) + "km]\n");
		SBNav.Append(" TRGT. SPEED [" + ShowSpeed + "m/s]\n");
		SBNav.Append(" TRGT. ALT [" + ShowAlt + "m]\n");
	}
	else if (CruiseEngaged)
	{
		SBNav.Append(" AUTOPILOT [CRUISE]\n");
		SBNav.Append(" CURRENT ROUTE [CRUISE]\n");
		SBNav.Append(" ST. [" + string_status + "] [" + CruiseWNum + "]\n");
		SBNav.Append(" CRUISE SPD [" + CruiseSpeed + "m/s]\n");
		SBNav.Append(" CRUISE ALT [" + CruiseAltitude + "m]\n");
	}
	else
	{
		SBNav.Append(" AUTOPILOT [OFF]\n");
		if (overrideType != "none")
		{
			if (overrideType == "takeoff_cords" || overrideType == "takeoff")
			{
				if (tp.LastILS == "" || rejectILSTakeoff) SBNav.Append(" AUTO TAKEOFF [AHEAD]\n");
				else SBNav.Append(" AUTO TAKEOFF [" + tp.LastILS + "]\n");
				SBNav.Append(" ST. [" + string_status + "]\n");
				SBNav.Append(" V-ROTATE [" + ShowSpeed + "m/s]\n");
				SBNav.Append(" V2 [" + V2 + "m/s]\n");
			}
			if (overrideType == "landing_cords" || overrideType == "landing")
			{
				if (tp.LastILS == "") SBNav.Append(" AUTOLAND [NO RUNWAY]\n");
				else SBNav.Append(" AUTOLAND [" + tp.LastILS + "]\n");
				SBNav.Append(" ST. [" + string_status + "]\n");
				SBNav.Append(" LANDING SPD [" + ShowSpeed + "m/s]\n");
				SBNav.Append(" DISTANCE [" + Math.Round(DistanceToPoint / 1000, 1) + "km]\n");
			}
			SBNav.Append(" TRGT. ALTITUDE [" + ShowAlt + "m]\n");
		}
		else { SBNav.Append(" CUR. ROUTE [" + CurrentRouteName + "]\n"); SBNav.Append(" CUR. WAYPOINT [" + WaypointNum + "]\n"); }
	}
}

public string GetNav()
{
	return SBNav.ToString();
}
#endregion

#region Takeoff
public bool Takeoff(bool isILS, string expectedCallsign, Vector3D RunwayStart, Vector3D RunwayStop)
{
	string_status = "TAKEOFF";
	if (isILS) tp.CheckILS(expectedCallsign, MyPos, out RunwayStart, out RunwayStop, Vector3D.Reject(RC.WorldMatrix.Forward, GravityVector));

	if (SavedRStart == new Vector3D(0, 0, 0))
	{
		if (RunwayStart != new Vector3D(0, 0, 0) && RunwayStart != RunwayStop)
		{
			PointToPointCourse = Vector3D.Normalize(RunwayStop - RunwayStart);
			double DistToRW = Math.Round(Vector3D.Distance(MyPos, RunwayStart + Vector3D.Dot(MyPos - RunwayStart, PointToPointCourse) * PointToPointCourse));
			if (DistToRW > 100) rejectILSTakeoff = true;
		}
		SavedRStart = MyPos; SavedRStop = SavedRStart + Vector3D.Reject(RC.WorldMatrix.Forward * 5000, GravityVector);
	}
	if (RunwayStart == new Vector3D(0, 0, 0) || RunwayStop == new Vector3D(0, 0, 0) || RunwayStart == RunwayStop || rejectILSTakeoff) { RunwayStart = SavedRStart; RunwayStop = SavedRStop; }

	AltAboveRW = (MyPos - RunwayStart).Dot(-GravityVector);
	RunwayAlt = Math.Round(MyAltitude - AltAboveRW);
	if (RC.DampenersOverride != AlwaysDampiners) RC.DampenersOverride = AlwaysDampiners;
	if (RC.HandBrake) RC.HandBrake = false;
	SetAirBrakes(false);
	LG.SetBrakes(false);

	PointToPointCourse = Vector3D.Normalize(RunwayStop - RunwayStart);
	if (UseGPWS) if (CheckGPWS()) if (!PullUp) { PullUp = true; if (TickCounter >= 10) { TickCounter = 0; SB.Request("GPWS"); } }
	if (SurfaceAlt > AltLimit + 10) PullUp = false;
	if (MyVelHor < VRotate) { DirToSlidingTarget = PointToPointCourse; PullUp = false; }
	else if (AltAboveRW > GearUpAlt || PullUp) { LG.GearUp(); DirToSlidingTarget = PointToPointCourse - GravityVector * MaxRadPitch; }
	else DirToSlidingTarget = PointToPointCourse - GravityVector * MathHelper.ToRadians(5);
	if (AltAboveRW > TakeoffEndAlt) { ResetBools(); return true; }

	if (V2 < VRotate || V2 - VRotate <= 5) V2 += 10;
	SetSpeed(V2, V2 - 5, V2 + 10);
	RotationAxis = CalcAxis(DirToSlidingTarget);
	Rotate(RotationAxis * 0.2);

	return false;
}
public void OverrideTakeoff(string callsign)
{
	overrideType = "takeoff";
	if (callsign == "") ReqCallsign = BasicILSCallsign;
	else ReqCallsign = callsign;
}
public void OverrideTakeoff(string PointFromName, string PointToName)
{
	Vector3D PointFrom, PointTo;
	PointTo = GetWaypointWithName(PointToName);
	if (PointTo == new Vector3D(0, 0, 0)) { SBErrors.Append("\n" + PointToName + " Waypoint Not Found!"); return; }
	PointFrom = GetWaypointWithName(PointFromName);
	if (PointFrom == new Vector3D(0, 0, 0)) { SBErrors.Append("\n" + PointFromName + " Waypoint Not Found!"); return; }
	SavedRStart = PointFrom; SavedRStop = PointTo;
	overrideType = "takeoff_cords";
}
#endregion
#region Landing
public bool Landing(bool isILS, string expectedCallsign, Vector3D RunwayStart, Vector3D RunwayStop, double DescendAngle)
{
	string_status = "LANDING";
	if (isILS) tp.CheckILS(expectedCallsign, MyPos, out RunwayStart, out RunwayStop, Vector3D.Zero);
	if (SavedRStart == new Vector3D(0, 0, 0)) { SavedRStart = RunwayStart; SavedRStop = RunwayStop; }
	if (RunwayStart == new Vector3D(0, 0, 0) || RunwayStop == new Vector3D(0, 0, 0) || RunwayStart == RunwayStop) { RunwayStart = SavedRStart; RunwayStop = SavedRStop; }
	if (RunwayStart == new Vector3D(0, 0, 0) || RunwayStop == new Vector3D(0, 0, 0) || RunwayStart == RunwayStop) { GoBackupPoints(); string_status = "GO BACKUP"; return false; }

	if (PullUp) { PointToPointCourse = Vector3D.Normalize(Vector3D.Reject(MyPos + RC.WorldMatrix.Forward * 5000 - MyPos, GravityVector)); DirToSlidingTarget = PointToPointCourse - GravityVector * MaxRadPitch; if (SurfaceAlt > (MyAltitude - RunwayAlt) / 4) PullUp = false; }
	else PointToPointCourse = Vector3D.Normalize(RunwayStop - RunwayStart);

	if (PrevRunwayStart == new Vector3D(0, 0, 0)) PrevRunwayStart = RunwayStart;
	Vector3D rwVel = (RunwayStart - PrevRunwayStart) / timeLimit;
	RunwayRelativeVel = Math.Round(Vector3D.Dot(MyVel, PointToPointCourse) - Vector3D.Dot(rwVel, PointToPointCourse), 2);
	PrevRunwayStart = RunwayStart;

	VecToRunwayStart = RunwayStart - MyPos;
	AltAboveRW = (MyPos - RunwayStart).Dot(-GravityVector);
	RunwayAlt = Math.Round(MyAltitude - AltAboveRW);
	DistanceToPoint = VecToRunwayStart.Dot(Vector3D.Normalize(RunwayStop - RunwayStart));

	double BackupAlt = Clamp(LastAltitude, RunwayAlt + 100, RunwayAlt + 1000);
	if (UseLandTCAS) if (!tp.CheckLandingPriority((int)DistanceToPoint, RunwayStart))
		{
			Vector3D oppositePoint = RunwayStart - PointToPointCourse * 10000;
			if (DistanceToPoint < 1500)
			{
				GoAround(RunwayStart, oppositePoint, BackupAlt); goAroundType = "out";
			}
			else if (DistanceToPoint >= 1500 && DistanceToPoint < 4500)
			{
				if (goAroundType == "none") goAroundType = "out";
				switch (goAroundType)
				{
					case "out": GoAround(RunwayStart, oppositePoint, BackupAlt); break;
					case "in": GoAround(oppositePoint, RunwayStart, BackupAlt); break;
					default: break;
				}
			}
			else if (DistanceToPoint >= 4500) { GoAround(oppositePoint, RunwayStart, BackupAlt); goAroundType = "in"; }
			return false;
		}

	Vector3D VecToRunwayStop = RunwayStop - MyPos;
	double StopRangeProjection = VecToRunwayStop.Dot(PointToPointCourse);
	double DistToRW = Math.Round(Vector3D.Distance(MyPos, RunwayStart + Vector3D.Dot(MyPos - RunwayStart, PointToPointCourse) * PointToPointCourse));
	//Parent.Echo("Distance To Runway: " + DistToRW);
	if ((StopRangeProjection < 0 && RunwayRelativeVel > 30) || DistanceToPoint < 0 && DistToRW >= 75) onGlideSlope = false;

	//Parent.Echo("DistToStop: " + StopRangeProjection + "\n OnGlideSlope: " + onGlideSlope);
	if (!onGlideSlope)
	{
		GoAround(RunwayStop, RunwayStart, BackupAlt);
		PointToPointCourse = Vector3D.Normalize(RunwayStop - RunwayStart);
		VecToRunwayStart = RunwayStart - MyPos;
		DistanceToPoint = VecToRunwayStart.Dot(PointToPointCourse);
		double DistToGlideSlopeStart = Clamp(BackupAlt / Math.Tan(MathHelper.ToRadians(DescendAngle)), 1000, 7500); //Parent.Echo("\n" + DistToGlideSlopeStart);
		if (DistanceToPoint >= DistToGlideSlopeStart) onGlideSlope = true;
		else return false;
	}

	LG.GearDown();

	SlidingTargetDist = Clamp(DistanceToPoint / 4, 100, 500);
	SlidingTarget = RunwayStart - PointToPointCourse * (DistanceToPoint - SlidingTargetDist);

	double h = Clamp(DistanceToPoint * MathHelper.ToRadians(DescendAngle), 10, 3000);

	SlidingTarget -= GravityVector * (h + PitchCorrection);
	if (!PullUp) DirToSlidingTarget = Vector3D.Normalize(SlidingTarget - MyPos);

	PitchCorrection = Clamp(5 * (MyVel.Dot(GravityVector) - DirToSlidingTarget.Dot(GravityVector)), -10, 10);


	//-------
	//LANDING
	//-------
	if (DistanceToPoint < 0 && DistToRW < 50)
	{
		PitchCorrection = 0;

		if (MyVelVert > -0.5 && MyVelVert < 0.5 && MyAltitude - RunwayAlt < 15)
		{
			DirToSlidingTarget = Vector3D.Normalize(PointToPointCourse * 20);
			RC.DampenersOverride = true;
			RC.HandBrake = true;
			LG.SetBrakes(true);
			SetAirBrakes(true);
			SetThrustP(0);
		}
		else
		{
			DirToSlidingTarget = Vector3D.Normalize(PointToPointCourse - GravityVector * MathHelper.ToRadians(3));
			double TouchVel = Clamp((-MyVelVert) * MinLandVelocity, 0, MinLandVelocity + 10);
			SetSpeed(TouchVel, TouchVel - 5, TouchVel + 1);
		}
		if (RunwayRelativeVel < 10 && RunwayRelativeVel > -10)
		{
			if (UseLandTCAS) tp.ClearTCAS("");
			SetAirBrakes(false);
			GyroOverride(false);
			SetThrustP(0);
			RC.HandBrake = true;
			RC.DampenersOverride = false;
			LG.SetBrakes(true);
			RunwayRelativeVel = 0;
			ResetBools();
			return true;
		}
	}
	else if (DistanceToPoint > 250 || rwVel.Length() > MaxLandVelocity) { if (UseLandGPWS) if (CheckGPWS(true, RunwayAlt)) if (!PullUp) { PullUp = true; if (TickCounter >= 10) { TickCounter = 0; SB.Request("GPWS"); } } SetSpeed(MaxLandVelocity + rwVel.Length(), MinLandVelocity, AirbrakeVelocity + rwVel.Length()); if (RC.DampenersOverride != AlwaysDampiners) RC.DampenersOverride = AlwaysDampiners; }
	else { SetSpeed(MaxLandVelocity, MinLandVelocity, AirbrakeVelocity); LG.SetBrakes(false); }

	//------
	//rotation
	//------

	RotationAxis = CalcAxis(DirToSlidingTarget);
	Rotate(RotationAxis); //* 3, gyros, RC

	return false;

}
public void OverrideLanding(string callsign)
{
	overrideType = "landing";
	if (callsign == "") ReqCallsign = BasicILSCallsign;
	else ReqCallsign = callsign;
}
public void OverrideLanding(string PointFromName, string PointToName)
{
	Vector3D PointFrom, PointTo;
	PointTo = GetWaypointWithName(PointToName);
	if (PointTo == new Vector3D(0, 0, 0)) { SBErrors.Append("\n" + PointToName + " Waypoint Not Found!"); return; }
	PointFrom = GetWaypointWithName(PointFromName);
	if (PointFrom == new Vector3D(0, 0, 0)) { SBErrors.Append("\n" + PointFromName + " Waypoint Not Found!"); return; }
	SavedRStart = PointFrom; SavedRStop = PointTo;
	overrideType = "landing_cords";
}
#endregion
#region GoToPoint
public bool GoToPoint(bool BackupRoute, Vector3D PointFrom, Vector3D PointTo, double Trg_h_asl, double Speed, bool UseThrust, bool UseRotate)
{
	if (BackupRoute) string_status = "GO BACKUP"; else string_status = "GO TO POINT";
	LG.GearUp();

	if (UseGPWS) if (CheckGPWS()) if (!PullUp) { PullUp = true; if (TickCounter >= 10) { TickCounter = 0; SB.Request("GPWS"); } }
	if (PullUp) { Trg_h_asl = MyAltitude + 2000; PointTo = MyPos + Vector3D.Reject(RC.WorldMatrix.Forward * 5000, GravityVector); PointFrom = MyPos; if (SurfaceAlt > AltLimit + 10) PullUp = false; }
	if (UseTCAS) Trg_h_asl += tp.CheckTCAS(true, MyPos, (int)Heading, (int)Trg_h_asl, (int)MyVelHor);
	if (RC.DampenersOverride != AlwaysDampiners) RC.DampenersOverride = AlwaysDampiners;
	PointToPointCourse = Vector3D.Normalize(PointTo - PointFrom);
	VecToRunwayStart = PointTo - MyPos;
	DistanceToPoint = VecToRunwayStart.Dot(PointToPointCourse);
	SlidingTargetDist = 5000;
	SlidingTarget = PointTo - PointToPointCourse * (DistanceToPoint - SlidingTargetDist);

	double DynamicClamp = Clamp(Math.Abs(Math.Round(Math.Log10(MyVelHor / 1000) * 1, 2)), 0, MaxRadPitch); //Parent.Echo(MyVelHor + "\n" + DynamicClamp);
	double PitchAngle = Clamp(((Trg_h_asl - MyAltitude) * 0.001), -DynamicClamp, DynamicClamp); //Parent.Echo("\n" + PitchAngle + "  " + MathHelper.ToDegrees(PitchAngle));

	Vector3D HorDirToTrg = Vector3D.Reject(SlidingTarget - MyPos, GravityVector);
	DirToSlidingTarget = Vector3D.Normalize(Vector3D.Normalize(HorDirToTrg) - GravityVector * PitchAngle);

	if (DistanceToPoint < 500)
	{
		if (UseTCAS) tp.ClearTCAS(""); PullUp = false; return true;
	}

	RotationAxis = CalcAxis(DirToSlidingTarget);
	if (UseRotate) Rotate(RotationAxis); // * 3 , gyros, RC
	if (UseThrust) SetSpeed(Speed, Speed - 5, Speed + 10);

	if (!BackupRoute) SetLastPoint(PointFrom, PointTo, Trg_h_asl, Speed);
	return false;
}
#endregion
#region Wait
public bool Wait(double seconds)
{
	string_status = "WAITING: " + Math.Round(time_for_wait, 1) + " / " + seconds + "s";

	time_for_wait += Runtime.TimeSinceLastRun.TotalSeconds / timeLimit;
	if (time_for_wait >= seconds)
	{
		time_for_wait = 0;
		return true;
	}
	return false;
}
#endregion

#region Backup Points
public void GoBackupPoints()
{
	switch (switchBackupPoints)
	{
		case true:
			if (GoToPoint(true, LastPointFrom, LastPointTo, LastAltitude, LastSpeed, true, true))
				switchBackupPoints = false;
			break;
		case false:
			if (GoToPoint(true, LastPointTo, LastPointFrom, LastAltitude, LastSpeed, true, true))
				switchBackupPoints = true;
			break;
	}
}
public void SetLastPoint(Vector3D lpFrom, Vector3D lpTo, double la, double ls)
{
	LastPointTo = lpTo;
	LastPointFrom = lpFrom;
	LastAltitude = la;
	LastSpeed = ls;
}
#endregion
#region GoAround
public void GoAround(Vector3D PointFrom, Vector3D PointTo, double Trg_h_asl)
{
	string_status = "GO AROUND";
	if (UseGPWS) if (CheckGPWS()) if (!PullUp) { PullUp = true; if (TickCounter >= 10) { TickCounter = 0; SB.Request("GPWS"); } }
	if (PullUp) { Trg_h_asl = MyAltitude + 2000; PointTo = MyPos + Vector3D.Reject(RC.WorldMatrix.Forward * 5000, GravityVector); PointFrom = MyPos; if (SurfaceAlt > AltLimit + 10) PullUp = false; }
	if (RC.DampenersOverride != AlwaysDampiners) RC.DampenersOverride = AlwaysDampiners;
	PointToPointCourse = Vector3D.Normalize(PointTo - PointFrom);
	VecToRunwayStart = PointTo - MyPos;
	SlidingTargetDist = 5000;
	DistanceToPoint = VecToRunwayStart.Dot(PointToPointCourse);

	SlidingTarget = PointTo - PointToPointCourse * (DistanceToPoint - SlidingTargetDist);

	if (UseTCAS) Trg_h_asl += tp.CheckTCAS(false, MyPos, (int)Heading, (int)Trg_h_asl, (int)MyVelHor);

	double DynamicClamp = Clamp(Math.Abs(Math.Round(Math.Log10(MyVelHor / 1000) * 1, 2)), 0, MaxRadPitch); //Parent.Echo(MyVelHor + "\n" + DynamicClamp);
	double PitchAngle = Clamp(((Trg_h_asl - MyAltitude) * 0.001), -DynamicClamp, DynamicClamp); //Parent.Echo("\n" + PitchAngle + "  " + MathHelper.ToDegrees(PitchAngle));

	Vector3D HorDirToTrg = Vector3D.Reject(SlidingTarget - MyPos, GravityVector);
	DirToSlidingTarget = Vector3D.Normalize(Vector3D.Normalize(HorDirToTrg) - GravityVector * PitchAngle);

	SetSpeed(V2, V2 - 5, V2 + 10);
	RotationAxis = CalcAxis(DirToSlidingTarget);
	Rotate(RotationAxis);
}
#endregion

#region Altitude
public void CalibrateAltitude()
{
	if (!RCReady || RC.Closed) return;
	RC.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Sealevel_Calibrate);
	Ini.Set("Storage", "Altitude Calibration", Sealevel_Calibrate);
	Me.CustomData = Ini.ToString();

}
void GetPlanetStats()
{
	if (!RCReady || RC.Closed) return;
	planetCenter = new Vector3D(0, 0, 0); RC.TryGetPlanetPosition(out planetCenter);
	planetDelta = planetCenter - RC.GetPosition();
	planetRadius = Math.Round(planetDelta.Length() - MyAltitude, 2);
	planetAngleDeg = 15 + (1 - 15) * ((Math.Log(planetRadius) - Math.Log(20000)) / (Math.Log(500000) - Math.Log(20000)));
}

public void GetAltitude()
{
	if (!RCReady || RC.Closed) return;
	double Pre_Alt = 0;
	RC.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Pre_Alt);
	MyAltitude = Math.Round(Math.Abs(Sealevel_Calibrate - Pre_Alt), 2);
	RC.TryGetPlanetElevation(MyPlanetElevation.Surface, out SurfaceAlt);
	SurfaceAlt = Math.Round(SurfaceAlt, 2);
}
#endregion
#region Heading
void CalculateHeading()
{
	Vector3D sunRotationAxis = new Vector3D(0, -1, 0);
	Vector3D eastVec = Vector3D.Cross(GravityVector, sunRotationAxis);
	Vector3D northVec = Vector3D.Cross(eastVec, GravityVector);
	Vector3D heading = Vector3D.Reject(RC.WorldMatrix.Forward, GravityVector);

	Heading = Math.Round(MathHelper.ToDegrees(Math.Acos(Clamp(heading.Dot(northVec) / Math.Sqrt(heading.LengthSquared() * northVec.LengthSquared()), -1, 1))), 1);

	if (Vector3D.Dot(RC.WorldMatrix.Forward, eastVec) < 0)
		Heading = 360 - Heading;
}
#endregion

#region GPWS
bool CheckGPWS(bool landing = false, double RWAlt = 0)
{

	double surfSpd = (LastSurfaceAlt - SurfaceAlt) / timeLimit; LastSurfaceAlt = SurfaceAlt;
	if (VelBuff.Count >= 5) { VelBuff.RemoveAt(0); VelBuff.Add(surfSpd); }
	else VelBuff.Add(surfSpd);

	double velocitySum = 0;
	foreach (var v in VelBuff) { velocitySum += v; }
	double terrainHeightDerivative = velocitySum / VelBuff.Count;
	double timeTillGroundCollision = SurfaceAlt / (terrainHeightDerivative);

	double altToRW = MyAltitude - RWAlt, SurfAppSpd = surfSpd - MyVelVert, debugMyAlt = SurfaceAlt * 0.035, debugMyAltMult = Clamp((0.5 + (-0.45 * (MyAltitude - 100)) / (15000 - 100)), 0.05, 0.5);
	AltLimit = MyAltitude * debugMyAltMult;
	double SAS_PD = VertVelPD.Control(SurfAppSpd), SAlt_PD = SurfVelPD.Control(SurfaceAlt);
	if (landing) { if (timeTillGroundCollision < 10 && SAS_PD > debugMyAlt && SAlt_PD < AltLimit && SurfaceAlt + altToRW / 2 < altToRW) return true; }
	else if (timeTillGroundCollision < 15 && SAS_PD > debugMyAlt && SAlt_PD < AltLimit) return true;
	return false;
}
#endregion

#region Speed Control
public void SetSpeed(double speed, double speedLowLimit, double SpeedUpLimit)
{
	if (speed > MyVelHor) ThrustPerc = (float)Clamp((speed - MyVelHor) / (speed - speedLowLimit), 0, 1); else ThrustPerc = 0;
	SetThrustP(ThrustPerc);
	if (MyVelHor > SpeedUpLimit) SetAirBrakes(true); else SetAirBrakes(false);
}
public void SetThrustP(float thrustP) { if (thrusters.Count > 0) foreach (var thr in thrusters) thr.ThrustOverridePercentage = thrustP; }
public void SetAirBrakes(bool open) { if (airbrakes.Count > 0) { if (open) foreach (IMyDoor airbrake in airbrakes) airbrake.OpenDoor(); else foreach (IMyDoor airbrake in airbrakes) airbrake.CloseDoor(); } }
#endregion
#region Axis Control
public Vector3D CalcAxis(Vector3D DirToTarget)
{
	if (double.IsNaN(DirToTarget.LengthSquared())) Parent.Initialize();
	int c = 0;
	while (c < 3 && (Vector3D.Normalize(Vector3D.Reject(DirToTarget, GravityVector)).Dot(RC.WorldMatrix.Forward) < Math.Cos(MaxRadPitch)))
	{
		c++;
		DirToTarget = Vector3D.Normalize(Vector3D.Reject(DirToTarget, GravityVector) + RC.WorldMatrix.Forward);
	}

	Vector3D preAxis = DirToTarget.Cross(RC.WorldMatrix.Forward);
	double pitchAngle = Math.Asin(Clamp(preAxis.Dot(RC.WorldMatrix.Right), -1, 1));
	double pitchSpeed = Clamp(pitchPID.Control(pitchAngle * PitchSpeedMultiplier), -MaxRadPitchSpd, MaxRadPitchSpd);
	double yawAngle = Math.Asin(Clamp(preAxis.Dot(RC.WorldMatrix.Up), -1, 1));
	double yawSpeed = Clamp(yawPID.Control(yawAngle * YawSpeedMultiplier), -MaxRadYawSpd, MaxRadYawSpd);
	Vector3D d = Vector3D.Reject(Vector3D.Normalize(MyVel), RC.WorldMatrix.Forward);
	if (d.Length() > 0.05)
		d = Vector3D.Normalize(d) * 0.05;
	d += (GravityVector * 0.05);
	double roll = d.Dot(RC.WorldMatrix.Right);
	double rollSpeed = Clamp(rollPID.Control(roll * RollSpeedMultiplier), -MaxRadRollSpd, MaxRadRollSpd);

	double RealRoll = Vector3D.Dot(RC.WorldMatrix.Right, GravityVector);
	if (RealRoll >= MaxRadRoll && rollSpeed < 0) { if (RealRoll >= MaxRadRoll + 0.08) rollSpeed = MaxRadRollSpd; else rollSpeed = 0; }
	else if (RealRoll <= -MaxRadRoll && rollSpeed > 0) { if (RealRoll <= -MaxRadRoll - 0.08) rollSpeed = -MaxRadRollSpd; else rollSpeed = 0; }

	double RealPitch = Vector3D.Dot(RC.WorldMatrix.Forward, GravityVector);
	if (RealPitch >= MaxRadPitch && pitchSpeed > 0) { if (RealPitch >= MaxRadPitch + 0.08) pitchSpeed = -MaxRadPitchSpd; else pitchSpeed = 0; }
	else if (RealPitch <= -MaxRadPitch && pitchSpeed < 0) { if (RealPitch <= -MaxRadPitch - 0.08) pitchSpeed = MaxRadPitchSpd; else pitchSpeed = 0; }

	preAxis += RC.WorldMatrix.Forward * rollSpeed;
	preAxis += RC.WorldMatrix.Up * yawSpeed;
	preAxis += RC.WorldMatrix.Right * pitchSpeed;
	return preAxis;
}
public void GyroOverride(bool Override)
{
	if (gyros.Count > 0)
		foreach (IMyGyro gyro in gyros)
		{
			gyro.SetValue("Override", Override);
			if (Override)
			{
				gyro.Yaw = 0;
				gyro.Pitch = 0;
				gyro.Roll = 0;
			}
		}
}
public void Rotate(Vector3D Axis)
{
	if (gyros.Count > 0)
		foreach (IMyGyro gyro in gyros)
		{
			gyro.SetValue("Override", true);
			gyro.Pitch = (float)Axis.Dot(gyro.WorldMatrix.Right);
			gyro.Yaw = (float)Axis.Dot(gyro.WorldMatrix.Up);
			gyro.Roll = (float)Axis.Dot(gyro.WorldMatrix.Backward);
		}
}
#endregion

#region ParseRoute

void ParseRoute(string rawRouteText, out string routename, out List<RoutePoint> route)
{
	route = new List<RoutePoint>();

	string[] lines = rawRouteText.Split('\n');
	routename = lines[0]; lines[0] = "";
	Vector3D pointFrom = new Vector3D();

	foreach (string line in lines)
	{
		string[] elements = line.Split(';');

		if (elements.Length == 0) continue;

		RoutePoint point = new RoutePoint();
		point.OperationType = elements[0];
		point.Distance = 0;
		switch (point.OperationType)
		{
			case "GoToPoint":
				if (elements.Length == 5)
				{
					Vector3D pointTo;
					if (!Vector3D.TryParse(elements[1], out pointTo)) pointTo = Vector3D.Zero;
					point.PointTo = pointTo;
					if (pointFrom != Vector3D.Zero)
					{
						if (pointFrom != pointTo) point.PointFrom = pointFrom;
						else point.PointFrom = MyPos;
					}
					else point.PointFrom = MyPos;

					pointFrom = pointTo;

					double temp_alt;
					if (double.TryParse(elements[2], out temp_alt)) point.Altitude = temp_alt;
					else point.Altitude = 0;

					double temp_spd;
					if (double.TryParse(elements[3], out temp_spd)) point.Speed = temp_spd;
					else point.Speed = 0;

					point.TimerName = elements[4];

					if (point.PointTo != Vector3D.Zero && point.PointFrom != Vector3D.Zero) point.Distance = (int)Math.Round(Vector3D.Distance(point.PointFrom, point.PointTo));
					route.Add(point);
				}
				else SBErrors.Append("\nPoint Parsing failed: " + line);
				break;
			case "Wait":
				if (elements.Length == 1)
				{
					point.Time = 0; point.TimerName = "default";
					route.Add(point);
				}
				else if (elements.Length == 2)
				{
					double temp_time;
					if (double.TryParse(elements[1], out temp_time)) point.Time = temp_time;
					else point.Time = 0;
					point.TimerName = "default";
					route.Add(point);
				}
				else if (elements.Length == 3)
				{
					double temp_time;
					if (double.TryParse(elements[1], out temp_time)) point.Time = temp_time;
					else point.Time = 0;
					point.TimerName = elements[2];
					route.Add(point);
				}
				else SBErrors.Append("\nPoint Parsing failed: " + line);
				break;
			case "Landing":
				if (elements.Length == 1)
				{
					point.ILS_TakeoffLanding = true; point.LandingAngle = 0; point.ExpectedCallsign = "default";
					point.TimerName = "default"; route.Add(point);
				}
				else if (elements.Length == 3)
				{
					point.ILS_TakeoffLanding = true;
					double temp_angle;
					if (double.TryParse(elements[1], out temp_angle)) point.LandingAngle = temp_angle;
					else point.LandingAngle = 0;
					point.ExpectedCallsign = elements[2];
					point.TimerName = "default";
					route.Add(point);
				}
				else if (elements.Length == 4)
				{
					point.ILS_TakeoffLanding = true;
					double temp_angle;
					if (double.TryParse(elements[1], out temp_angle)) point.LandingAngle = temp_angle;
					else point.LandingAngle = 0;
					point.ExpectedCallsign = elements[2];
					point.TimerName = elements[3];
					route.Add(point);
				}
				else if (elements.Length == 5)
				{
					Vector3D _pointFrom;
					if (Vector3D.TryParse(elements[1], out _pointFrom)) point.PointFrom = _pointFrom;
					Vector3D _pointTo;
					if (Vector3D.TryParse(elements[2], out _pointTo)) { point.PointTo = _pointTo; pointFrom = _pointTo; }
					point.ILS_TakeoffLanding = false;

					double temp_angle;
					if (double.TryParse(elements[3], out temp_angle)) point.LandingAngle = temp_angle;
					else point.LandingAngle = 0;

					point.ExpectedCallsign = "default";
					point.TimerName = elements[4];

					if (point.PointTo != Vector3D.Zero && point.PointFrom != Vector3D.Zero) point.Distance = (int)Math.Round(Vector3D.Distance(point.PointFrom, point.PointTo));
					route.Add(point);
				}
				else SBErrors.Append("\nPoint Parsing failed: " + line);
				break;
			case "Takeoff":
				if (elements.Length == 1) { point.ILS_TakeoffLanding = true; point.ExpectedCallsign = "default"; point.TimerName = "default"; route.Add(point); }
				else if (elements.Length == 2) { point.ILS_TakeoffLanding = true; point.ExpectedCallsign = elements[1]; point.TimerName = "default"; route.Add(point); }
				else if (elements.Length == 3) { point.ILS_TakeoffLanding = true; point.ExpectedCallsign = elements[1]; point.TimerName = elements[2]; route.Add(point); }
				else if (elements.Length == 4)
				{
					Vector3D _pointFrom;
					if (Vector3D.TryParse(elements[1], out _pointFrom)) point.PointFrom = _pointFrom;
					Vector3D _pointTo;
					if (Vector3D.TryParse(elements[2], out _pointTo)) { point.PointTo = _pointTo; pointFrom = _pointTo; }
					point.ILS_TakeoffLanding = false;
					point.TimerName = elements[3];
					if (point.PointTo != Vector3D.Zero && point.PointFrom != Vector3D.Zero) point.Distance = (int)Math.Round(Vector3D.Distance(point.PointFrom, point.PointTo));
					route.Add(point);
				}
				else SBErrors.Append("\nPoint Parsing failed: " + line);
				break;
			default:
				break;
		}
	}
}
#endregion
#region GenerateRoute

void GenerateRoute(bool Circle, Vector3D startPoint, Vector3D endPoint, out List<RoutePoint> routePoints, double desiredHeight = 2500, double dAngle = 3)
{
	routePoints = new List<RoutePoint>();
	if (!RCReady || RC.Closed) return;
	Vector3D pointFrom = startPoint;
	GetPlanetStats();
	Vector3D StartToCenter = Vector3D.Normalize(startPoint - planetCenter);
	double StartAlt = Math.Round(Vector3D.Distance(startPoint, planetCenter) - planetRadius);
	Vector3D EndToCenter = Vector3D.Normalize(endPoint - planetCenter);
	double EndAlt = Math.Round(Vector3D.Distance(endPoint, planetCenter) - planetRadius);
	Vector3D normal = Vector3D.Cross(StartToCenter, EndToCenter);
	double angle;
	if (!Circle) angle = Math.Acos(Vector3D.Dot(StartToCenter, EndToCenter));
	else angle = MathHelper.ToRadians(360);
	double maxAltInRoute = 0;
	double deltaAngle = Math.Round(MathHelper.ToRadians(dAngle), 6);
	int numPoints = (int)Math.Ceiling(angle / deltaAngle);
	double currentHeight = 0; int numToDescend = 0; string text = "";
	if (!Circle)
	{
		double SurfaceAlt; RC.TryGetPlanetElevation(MyPlanetElevation.Surface, out SurfaceAlt);
		if (MyVelHor < 30 || SurfaceAlt < 100)
		{
			RoutePoint point = new RoutePoint { ExpectedCallsign = BasicILSCallsign, TimerName = AutopilotTimerName, OperationType = "Takeoff", ILS_TakeoffLanding = true, PointTo = new Vector3D(0, 0, 0), PointFrom = new Vector3D(0, 0, 0) };
			routePoints.Add(point);
		}
	}
	for (int i = 1; i <= numPoints; i++)
	{
		double currentAngle = i * deltaAngle;

		MatrixD rotationMatrix = MatrixD.CreateFromAxisAngle(Vector3D.Normalize(normal), currentAngle);
		Vector3D currentPoint = Vector3D.Rotate(StartToCenter, rotationMatrix) * planetRadius + planetCenter;
		Vector3D PointToCenter = Vector3D.Normalize(currentPoint - planetCenter);
		double dist = Math.Round(Vector3D.Reject(currentPoint - pointFrom, GravityVector).Length());

		if (!Circle)
		{
			double offset = Math.Round(dist * Math.Tan(MathHelper.ToRadians(Clamp(MaxPitchAngle - 3, 1, 89))));
			if (offset > desiredHeight) offset = desiredHeight;
			if (i == 1)
			{
				if (Math.Abs(StartAlt - desiredHeight) > offset) currentHeight = StartAlt;
				numToDescend = (int)Math.Ceiling(Math.Abs(EndAlt - desiredHeight) / offset); text += numToDescend + "\n"; if (Math.Abs(numToDescend * offset - EndAlt) >= desiredHeight) numToDescend -= 1;
				if (numToDescend > numPoints / 2) numToDescend = numPoints / 2;
				text += numToDescend + "\n";
			}

			if (numPoints - i > numToDescend) { if (Math.Abs(currentHeight - desiredHeight) >= offset) currentHeight += offset; else currentHeight = desiredHeight; }
			else if (Math.Abs(currentHeight - EndAlt) >= offset * (1 + (0.1 - 1) * ((Clamp(MaxPitchAngle, 30, 90) - 30) / (90 - 30)))) currentHeight -= offset;
			if (i == numPoints - 1) currentHeight /= 2;
			if (currentHeight < EndAlt + 1000 || i == numPoints) currentHeight = EndAlt + 1000;
			text += dist + " dist   " + offset + " offset  " + (numPoints - i) + " num   " + currentHeight + " height\n";
			if (currentHeight > maxAltInRoute) maxAltInRoute = currentHeight;
			currentPoint += PointToCenter * currentHeight;
		}
		else currentPoint += PointToCenter * desiredHeight;

		dist = (int)Math.Round(Vector3D.Distance(pointFrom, currentPoint));
		RoutePoint point = new RoutePoint { OperationType = "GoToPoint", PointFrom = pointFrom, Altitude = currentHeight, Speed = 0 };
		if (!Circle) point.TimerName = AutopilotTimerName; else point.TimerName = "default";
		if (i < numPoints)
		{ point.Distance = dist; point.PointTo = currentPoint; if (Math.Abs(currentHeight - desiredHeight) > desiredHeight * 0.1) point.Speed = V2; }
		else
		{ point.PointTo = endPoint; point.Distance = (int)Math.Round(Vector3D.Distance(pointFrom, endPoint)); if (!Circle) point.Speed = V2; }
		pointFrom = currentPoint;
		routePoints.Add(point);
	}
	if (!Circle)
	{

		int numDescendPoints = 0, numRepeatPoints = 0, iter = routePoints.Count() - 1; double height = routePoints[iter].Altitude;
		while (height < maxAltInRoute)
		{
			height = routePoints[iter].Altitude;
			if (height < maxAltInRoute)
			{
				numDescendPoints++;
				if (routePoints[iter - 2] != null && routePoints[iter].Altitude == routePoints[iter - 1].Altitude) if (MaxRadPitch > Math.Atan((routePoints[iter - 2].Altitude - routePoints[iter - 1].Altitude) / Vector3D.Distance(routePoints[iter].PointTo, routePoints[iter - 1].PointTo))) numRepeatPoints++;
			}
			iter--;
		}
		text += "\n" + numDescendPoints + " des " + numRepeatPoints + " rep " + height + " h\n";
		for (int i = 0; i < numRepeatPoints; i++)
		{
			for (int j = routePoints.Count() - 1; j > routePoints.Count() - numDescendPoints - 1; j--)
			{
				routePoints[j].PointTo += Vector3D.Normalize(routePoints[j].PointTo - planetCenter) * (routePoints[j - 1].Altitude - routePoints[j].Altitude);
				text += (routePoints[j - 1].Altitude - routePoints[j].Altitude) + " RAZN  ";
				routePoints[j].Altitude = routePoints[j - 1].Altitude;
				if (routePoints[j].Altitude == maxAltInRoute) routePoints[j].Speed = 0;
				text += routePoints[j].Altitude + " ALT\n";
			}
		}
		foreach (var p in routePoints) { if (p.Altitude == maxAltInRoute) p.Altitude = 0; p.PointTo = Vector3D.Round(p.PointTo, 2); }
		text += "\n";

		RoutePoint pointLanding = new RoutePoint
		{
			TimerName = AutopilotTimerName,
			OperationType = "Landing",
			ILS_TakeoffLanding = true,
			ExpectedCallsign = BasicILSCallsign,
			LandingAngle = 0,
			PointTo = new Vector3D(0, 0, 0),
			PointFrom = new Vector3D(0, 0, 0)
		};
		routePoints.Add(pointLanding);
	}
	var Debug = Parent.GridTerminalSystem.GetBlockWithName("!debug") as IMyTextSurface;
	for (int i = 0; i < routePoints.Count(); i++)
	{
		text += routePoints[i].OperationType + ";GPS:" + i + ":" + routePoints[i].PointTo.X + ":" + routePoints[i].PointTo.Y + ":" + routePoints[i].PointTo.Z + ":#F17575:" + routePoints[i].Altitude + ";" + routePoints[i].Speed + ";" + routePoints[i].Distance + ";" + routePoints[i].TimerName + "\n";
	}
	Debug?.WriteText(text);
}
public void BuildRoute(bool Circle, string PointToName, string PointFromName = "")
{
	Vector3D PointFrom, PointTo;
	PointTo = GetWaypointWithName(PointToName);
	if (PointTo == new Vector3D(0, 0, 0)) { SBErrors.Append("\n" + PointToName + " Waypoint Not Found!"); return; }
	if (PointFromName != "")
	{
		PointFrom = GetWaypointWithName(PointFromName);
		if (PointFrom == new Vector3D(0, 0, 0)) { SBErrors.Append("\n" + PointFromName + " Waypoint Not Found!"); return; }
	}
	else PointFrom = MyPos;

	CurrentRoute.Clear(); WaypointNum = 0; RepeatRoute = Circle; Ini.Set("Autopilot Settings", "Repeat Route", RepeatRoute); Parent.Me.CustomData = Ini.ToString();
	GenerateRoute(Circle, PointFrom, PointTo, out CurrentRoute, BasicAPAltitude, planetAngleDeg);
	CurrentRouteName = "TO " + PointToName;
	SaveRouteToStorage();
}
Vector3D GetWaypointWithName(string name)
{
	Vector3D waypoint = new Vector3D(0, 0, 0);
	if (WaypointsBlock == null || WaypointsBlock.Closed)
	{
		SBErrors.Append("\n" + WBName + " Block Not Found! No way to get a waypoint!"); return waypoint;
	}
	string[] lines = WaypointsBlock.CustomData.Split('\n');
	foreach (string line in lines)
	{
		string[] elements = line.Split(':');
		if (elements.Length < 5) continue;
		if (elements[1] == name)
		{
			double X, Y, Z;
			if (double.TryParse(elements[2], out X) && double.TryParse(elements[3], out Y) && double.TryParse(elements[4], out Z))
			{ waypoint = new Vector3D(X, Y, Z); return waypoint; }
		}
	}
	return waypoint;
}
public void RecordRoutePoint()
{
	if (WaypointsBlock == null || WaypointsBlock.Closed)
	{
		SBErrors.Append("\n" + WBName + " Block Not Found! No way to record a waypoint!"); return;
	}
	WaypointsBlock.CustomData += "\nGoToPoint;" + Vector3D.Round(MyPos, 2).ToString() + ";" + MyAltitude + ";" + MyVelHor + ";" + AutopilotTimerName;
	SB.Request("ObjComp");
}
#endregion
#region Export Route
public void ExportRoute(string blockname)
{
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
	Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomName.Contains(blockname));
	IMyTerminalBlock Block;
	if (Blocks.Count > 0) Block = Blocks.First(); else { SBErrors.Append("\n" + blockname + " Not Found!"); return; }
	Block.CustomData = CurrentRouteName + "\n";
	foreach (var point in CurrentRoute)
	{
		Block.CustomData += point.OperationType + ";";
		switch (point.OperationType)
		{
			case "GoToPoint":
				Block.CustomData += point.PointTo.ToString() + ";" + point.Altitude + ";" + point.Speed + ";";
				break;
			case "Wait":
				Block.CustomData += point.Time + ";";
				break;
			case "Landing":
				if (point.PointFrom != Vector3D.Zero && point.PointTo != Vector3D.Zero)
				{
					Block.CustomData += point.PointFrom.ToString() + ";" + point.PointTo.ToString() + ";" + point.LandingAngle + ";";
				}
				else Block.CustomData += point.LandingAngle + ";" + point.ExpectedCallsign + ";";
				break;
			case "Takeoff":
				if (point.PointFrom != Vector3D.Zero && point.PointTo != Vector3D.Zero)
				{
					Block.CustomData += point.PointFrom.ToString() + ";" + point.PointTo.ToString() + ";";
				}
				else Block.CustomData += point.ExpectedCallsign + ";";
				break;
		}
		Block.CustomData += point.TimerName + "\n";
	}
}
#endregion
#region Import Route
public void ImportRoute(string blockname)
{
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
	Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomName.Contains(blockname));
	IMyTerminalBlock Block;
	if (Blocks.Count > 0) Block = Blocks.First(); else { SBErrors.Append("\n" + blockname + " Not Found!"); return; }
	CurrentRoute.Clear(); WaypointNum = 0;
	ParseRoute(Block.CustomData, out CurrentRouteName, out CurrentRoute);
	CalculateFullDistance();
	SaveRouteToStorage();
}
void SaveRouteToStorage()
{
	Parent.Storage = CurrentRouteName + "\n";
	foreach (var point in CurrentRoute)
	{
		Parent.Storage += point.OperationType + ";";
		switch (point.OperationType)
		{
			case "GoToPoint":
				Parent.Storage += point.PointTo.ToString() + ";" + point.Altitude + ";" + point.Speed + ";";
				break;
			case "Wait":
				Parent.Storage += point.Time + ";";
				break;
			case "Landing":
				if (point.PointFrom != Vector3D.Zero && point.PointTo != Vector3D.Zero)
				{
					Parent.Storage += point.PointFrom.ToString() + ";" + point.PointTo.ToString() + ";" + point.LandingAngle + ";";
				}
				else Parent.Storage += point.LandingAngle + ";" + point.ExpectedCallsign + ";";
				break;
			case "Takeoff":
				if (point.PointFrom != Vector3D.Zero && point.PointTo != Vector3D.Zero)
				{
					Parent.Storage += point.PointFrom.ToString() + ";" + point.PointTo.ToString() + ";";
				}
				else Parent.Storage += point.ExpectedCallsign + ";";
				break;
		}
		Parent.Storage += point.TimerName + "\n";
	}
}
#endregion
#region Distance
void CalculateFullDistance()
{
	FullDistance = 0;
	if (CurrentRoute.Count() > 0) foreach (var point in CurrentRoute) FullDistance += point.Distance;
}
double CalculateDistanceLeft()
{
	double distance = 0;
	distance += Math.Round(DistanceToPoint, 2);
	if (WaypointNum != lastWaypointNum && CurrentRoute.Count() > 0) { SavedDistance = 0; for (int i = WaypointNum + 1; i < CurrentRoute.Count(); i++) SavedDistance += CurrentRoute[i].Distance; }
	distance += SavedDistance;
	return distance;
}
double CalculateDistancePassed()
{
	double distance = 0;
	if (CurrentRoute.Count() >= WaypointNum + 1)
	{
		distance += Math.Round(CurrentRoute[WaypointNum].Distance - DistanceToPoint);
		if (WaypointNum != lastWaypointNum) { SavedDistance2 = 0; for (int i = 0; i < WaypointNum; i++) SavedDistance2 += CurrentRoute[i].Distance; }
		distance += SavedDistance2;
	}
	return distance;
}
#endregion
#region RoutePoint
class RoutePoint
{
	public Vector3D PointTo { get; set; }
	public Vector3D PointFrom { get; set; }
	public double Distance { get; set; }
	public string OperationType { get; set; }
	public string ExpectedCallsign { get; set; }
	public bool ILS_TakeoffLanding { get; set; }
	public double LandingAngle { get; set; }
	public double Altitude { get; set; }
	public double Speed { get; set; }
	public double Time { get; set; }
	public string TimerName { get; set; }
}
#endregion

#region Tools
double Clamp(double value, double min, double max)
{
	value = ((value > max) ? max : value);
	value = ((value < min) ? min : value);
	return value;
}
public void ReleaseControls()
{
	SetAirBrakes(false); SetThrustP(0); GyroOverride(false);
}
public void SwitchAP(bool status)
{
	if (ApEngaged == status) return;
	ApEngaged = status; Ini.Set("Storage", "Autopilot Enabled", ApEngaged); Me.CustomData = Ini.ToString();
	if (ApEngaged == false) { ReleaseControls(); SB.Request("AutoStop"); } else SB.Request("AutoStart");
}
public void SwitchCruise(bool status)
{
	if (CruiseEngaged == status) return;
	CruiseEngaged = status; Ini.Set("Storage", "Cruise Control Enabled", CruiseEngaged); Me.CustomData = Ini.ToString();
	if (CruiseEngaged)
	{
		CruiseRoute.Clear();
		RecordCruiseAlt(); RecordCruiseSpd();
	}
	if (!CruiseEngaged) { ReleaseControls(); SB.Request("AutoStop"); }
	else SB.Request("AutoStart");
}
void RecordCruiseAlt()
{
	CruiseAltitude = MyAltitude;
	Ini.Set("Storage", "Cruise Saved Alt", CruiseAltitude); Me.CustomData = Ini.ToString();
}
void RecordCruiseSpd()
{
	CruiseSpeed = MyVelHor;
	Ini.Set("Storage", "Cruise Saved Speed", CruiseSpeed); Me.CustomData = Ini.ToString();
}
void SaveWNum()
{
	if (WaypointNum != lastWaypointNum)
	{
		Ini.Set("Storage", "Current Waypoint", WaypointNum);
		Me.CustomData = Ini.ToString();
		lastWaypointNum = WaypointNum;
		ResetBools();
	}
}
public void ResetBools() { PrevRunwayStart = SavedRStart = SavedRStop = new Vector3D(0, 0, 0); onGlideSlope = true; rejectILSTakeoff = false; PullUp = false; tp.ClearLastILS(); }
IMyShipController GetControlledShipController()
{
	if (controllers.Count == 0)
		return null;

	foreach (IMyShipController thisController in controllers)
	{
		if (thisController.IsUnderControl && thisController.CanControlShip)
			return thisController;
	}
	return RC;
}
public void Repeat(bool rep) { Ini.Set("Autopilot Settings", "Repeat Route", rep); Me.CustomData = Ini.ToString(); }
public void ResetInput()
{
	ClickCounter = 0;
}
public void ClearErrorList() { SBErrors.Clear(); }
public bool ErrListNotEmpty() { if (SBErrors.Length > 0) return true; else return false; }
public string GetErrorsList() { return SBErrors.ToString(); }
#endregion
#region PID
class PID
{
	double _kP = 0, _kI = 0, _kD = 0, _integralDecayRatio = 0, _lowerBound = 0, _upperBound = 0, _timeStep = 0, _inverseTimeStep = 0, _errorSum = 0, _lastError = 0;
	bool _firstRun = true, _integralDecay = false;
	public double Value { get; private set; }
	public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep)
	{ _kP = kP; _kI = kI; _kD = kD; _lowerBound = lowerBound; _upperBound = upperBound; _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; _integralDecay = false; }
	public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
	{ _kP = kP; _kI = kI; _kD = kD; _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; _integralDecayRatio = integralDecayRatio; _integralDecay = true; }
	public double Control(double error)
	{
		//Compute derivative term
		var errorDerivative = (error - _lastError) * _inverseTimeStep;
		if (_firstRun) { errorDerivative = 0; _firstRun = false; }
		//Compute integral term
		if (!_integralDecay) { _errorSum += error * _timeStep; _errorSum = MathHelper.Clamp(_errorSum, _lowerBound, _upperBound); }
		else { _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep; }
		_lastError = error;
		//Construct output
		this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
		return this.Value;
	}
	public double Control(double error, double timeStep) { _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; return Control(error); }
	public void Reset() { _errorSum = 0; _lastError = 0; _firstRun = true; }
}
#endregion
}
//=== End Of Script ===
}
}
