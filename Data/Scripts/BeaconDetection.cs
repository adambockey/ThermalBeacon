using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using VRage.Game;
using Sandbox.Game.EntityComponents;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using Sandbox.Game.Entities;
using SpaceEngineers.Game.ModAPI;

namespace ThermalShipBeaconV3
{
    //[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "DetectionSmallBlockBeacon", "DetectionLargeBlockBeacon")]
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "LargeBlockBeacon", "SmallBlockBeacon")]
    public class BeaconDetect : MyGameLogicComponent
    {

        IMyTerminalBlock Beacon;
        private List<IMyPowerProducer> PowerProducers = new List<IMyPowerProducer>();
        private List<IMyThrust> ThermalProducers = new List<IMyThrust>();
        private List<IMyVirtualMass> HeatSinks = new List<IMyVirtualMass>();
        private MyObjectBuilder_EntityBase m_objectBuilder;
        private MyDefinitionId electricity = MyResourceDistributorComponent.ElectricityId;
        private IMyCubeGrid CubeGrid = null;
        private bool MessageSent = false;
        private bool SpamMessageSent = false;
        private bool unloadHandlers = false;
        private int lastRun = 6;
        private float lastOutput = 0;
        private int delay = 0;
        //Block OFF Coding
        public static IMyTerminalBlock m_block = null;
        IMyBeacon m_beacon = null;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            m_objectBuilder = objectBuilder;
            m_block = Entity as IMyTerminalBlock;
            m_beacon = m_block as IMyBeacon;
            //Initilize enabledChangedEvent
            m_beacon.EnabledChanged += M_beacon_EnabledChanged;

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        private void M_beacon_EnabledChanged(IMyTerminalBlock obj)
        {
            m_beacon = m_beacon as IMyBeacon;

            //Prevent calling myself
            if (m_beacon.Enabled == true)
                return;

            m_beacon.Enabled = true;
            //throw new NotImplementedException();
            //m_beacon.EnabledChanged -= M_beacon_EnabledChanged;
        }

        public override void Close()
        {
            m_beacon.EnabledChanged -= M_beacon_EnabledChanged;
        }

        private void CubeGrid_UpdatePowerGrid(IMySlimBlock obj)
        {
            CubeGrid = obj.CubeGrid;
            var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(CubeGrid);
            gts.GetBlocksOfType(PowerProducers, block =>
            {
                if (block.IsSameConstructAs(Beacon))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });

            gts.GetBlocksOfType(ThermalProducers, block =>
            {
                if (block is IMyThrust && block.IsSameConstructAs(Beacon))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });

            gts.GetBlocksOfType(HeatSinks, block =>
            {
                if (block is IMyVirtualMass && block.IsSameConstructAs(Beacon))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });



            if (PowerProducers.Count != 0 || ThermalProducers.Count != 0)
            {
                CubeGrid.OnBlockAdded += CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockRemoved += CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockIntegrityChanged += CubeGrid_RefreshThermalGenerators;
                unloadHandlers = true;
            }
        }

        private void CubeGrid_RefreshThermalGenerators(IMySlimBlock obj)
        {
            if (obj is IMyBeacon)
            {
                Beacon_CheckThermal((IMyEntity)obj);
            }
        }
        private float GetBeaconRadius()
        {
            if (Beacon != null)
            {
                return ((IMyBeacon)Beacon).Radius;
            }
            else
            {
                return 0.0f;
            }
        }
        public override void UpdateAfterSimulation100()
        {
            if (lastRun >= 6)
            {
                lastRun = 0;
                if (Beacon == null)
                {
                    Beacon = Entity as IMyTerminalBlock;
                    //grid maintenance. 
                    CubeGrid = Beacon.CubeGrid;
                }

                if (PowerProducers.Count == 0 || ThermalProducers.Count == 0)
                {
                    CubeGrid_UpdatePowerGrid(Beacon.SlimBlock);
                }

                if (Beacon != null)
                {
                    try
                    {
                        if (Beacon.IsWorking)
                        {
                            Beacon_CheckThermal(Beacon);
                        }
                        else
                        {
                            {
                                CubeGrid_UpdatePowerGrid(Beacon.SlimBlock);
                                if (GetThermalOutput(Beacon) != 0.0f && GetThermalOutput(Beacon) != lastOutput && GetThermalOutput(Beacon) != calculateRadius(GetThermalOutput(Beacon))) //Checks for zero power grids
                                {
                                    //if (GetThermalOutput(Beacon) <= 5)
                                    //{
                                    //ApplyDamagePower(Beacon);
                                    TogglePower(Beacon);
                                    //}
                                }
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        MyLog.Default.WriteLine(exc);
                    }
                }
            }
            lastRun += 1;
        }

        private void Beacon_CheckThermal(VRage.ModAPI.IMyEntity obj)
        {
            if (Beacon != null)
            {
                var subtype = Beacon.Name;
                if (obj is IMyBeacon && obj != null)
                {
                    var output = calculateRadius(GetThermalOutput(Beacon));
                    var beacon = obj as IMyBeacon;

                    if (lastOutput > output)
                    {
                        delay = 0;
                        output = lastOutput * 0.95f;
                    }

                    if (output <= 1.0f)
                    {
                        output = 1.0f;
                    }

                    if (GetBeaconRadius() < output && output != 0.0f && (GetBeaconRadius() != lastOutput))
                    {
                        delay += 1;
                        CubeGrid_UpdatePowerGrid(Beacon.SlimBlock);
                        if (delay == 5)
                        {
                            //ApplyDamagePower(Beacon);
                            if (output <= 500000)// && output <= 5)
                            {
                                TogglePower(Beacon);
                            }
                        }
                    }

                    lastOutput = output;
                    beacon.Radius = output;
                    //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "beacon.Radius Output Range:" + output.ToString());
                    beacon.Enabled = true;

                    if (beacon.Radius <= 8000)
                    {
                        beacon.HudText = "Small Signature";
                    }

                    if (beacon.Radius >= 8001 && beacon.Radius <= 100000)
                    {
                        beacon.HudText = "Medium Signature";
                    }

                    if (beacon.Radius >= 100001 && beacon.Radius <= 250000)
                    {
                        beacon.HudText = "Large Signature";
                    }

                    if (beacon.Radius >= 250001 && beacon.Radius <= 400000)
                    {
                        beacon.HudText = "Huge Signature";
                    }

                    if (beacon.Radius >= 400001)
                    {
                        beacon.HudText = "Massive Signature";
                    }
                }
            }
        }

        private void TogglePower(IMyTerminalBlock _b)
        {
            if (_b == null)
                return;
            foreach (var power in PowerProducers)
            {
                var grid = power.Parent as MyCubeGrid;
                if (grid != null)// && grid.Physics != null)
                {
                    try
                    {
                        if (power == null)
                            return;
                        //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "Disabled Power");
                        power.Enabled = false;
                    }
                    catch (Exception exc)
                    {
                        MyLog.Default.WriteLine(exc);
                    }
                }
            }
        }

        private void BroadcastDamageMessage(IMyTerminalBlock _b)
        {
            if (_b == null)
                return;
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if (players != null)
            {
                var sphere = new BoundingSphereD(_b.PositionComp.WorldAABB.Center, 250);
                var sendMessage = false;
                foreach (var player in players)
                {
                    if (player != null && player.IdentityId != MyAPIGateway.Session.Player.IdentityId) continue;
                    if (player != null && !sphere.Intersects(player.Character.WorldVolume)) continue;
                    sendMessage = true;
                    break;
                }

                if (sendMessage)
                {
                    if (!MessageSent)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Beacon Tampering Detected: Power System Damaged.", 9600, "Red");
                        MessageSent = true;
                    }
                }
                else
                {
                    //MyAPIGateway.Utilities.ShowNotification("Player NO.", 9600, "Red");
                }
            }
        }

        private void BroadcastSpamMessage(IMyTerminalBlock _b)
        {
            if (_b == null)
                return;
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if (players != null)
            {
                var sphere = new BoundingSphereD(_b.PositionComp.WorldAABB.Center, 500);
                var sendMessage = false;
                foreach (var player in players)
                {
                    if (player != null && player.IdentityId != MyAPIGateway.Session.Player.IdentityId) continue;
                    if (player != null && !sphere.Intersects(player.Character.WorldVolume)) continue;
                    sendMessage = true;
                    break;
                }

                if (sendMessage)
                {
                    if (!SpamMessageSent)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Coolant System Overloaded: Too many Heat Sinks on " + _b.CubeGrid.DisplayName.ToString() , 9600, "Red");
                        SpamMessageSent = true;
                    }
                }
                else
                {
                    //MyAPIGateway.Utilities.ShowNotification("Player NO.", 9600, "Red");
                }
            }
        }

        private float calculateRadius(float EnergyinMW)
        {
            double radius = 0.0d;
            double rawRadius = 0.0d;
            {
                rawRadius = EnergyinMW * 10000;
                radius = Math.Round(rawRadius, 4);
            }
            return (float)radius;
        }

        public override void OnRemovedFromScene()
        {
            base.OnRemovedFromScene();
            if (unloadHandlers)
            {
                CubeGrid.OnBlockAdded -= CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockRemoved -= CubeGrid_RefreshThermalGenerators;
                CubeGrid.OnBlockIntegrityChanged -= CubeGrid_RefreshThermalGenerators;
            }
        }
        private float GetThermalOutput(IMyTerminalBlock block)
        {
            double rawThermalOutput = 0.0d;
            //static grid logic
            //if (block.CubeGrid.IsStatic)
            //{
            //    return 0.0f;
            //}

            if (PowerProducers.Count != 0)
            {
                foreach (var powerProducer in PowerProducers)
                {
                    if (powerProducer is IMyReactor)
                    {
                        rawThermalOutput += powerProducer.CurrentOutput / 24; //NEEDS RATIO DATA
                    }
                    else if (powerProducer is IMyBatteryBlock)
                    {
                        //added to retrieve input power
                        var _battery = powerProducer as IMyBatteryBlock;
                        //subtract battery input before output
                        rawThermalOutput -= _battery.CurrentInput / 24 * 0.35f;
                        rawThermalOutput += powerProducer.CurrentOutput / 24 * 0.35f; //NEEDS RATIO DATA
                    }
                    else if (powerProducer.BlockDefinition.SubtypeId.ToLower().Contains("engine")) //because thank you, Keen.
                    {
                        rawThermalOutput += powerProducer.CurrentOutput / 24 * 0.65f; //NEEDS RATIO DATA
                    }
                    else  //Modded Blocks, Wind & Fucking Solar Panels
                    {
                        rawThermalOutput += powerProducer.CurrentOutput / 24 * 0.15f; //NEEDS RATIO DATA
                    }
                }
            }

            if (ThermalProducers.Count != 0)
            {
                foreach (var thermalProducer in ThermalProducers)
                {
                    //thruster logic
                    if (thermalProducer is IMyThrust)
                    {
                        var thrust = thermalProducer as IMyThrust;
                        if (thrust.BlockDefinition.SubtypeId.ToLower().Contains("hydrogen"))
                        {
                            rawThermalOutput += (thrust.CurrentThrust / 4500000); //NEEDS RATIO DATA
                        }
                    }
                }
            }

            if (HeatSinks.Count != 0 && HeatSinks.Count <= 50)
            {
                foreach (var heatsink in HeatSinks)
                {
                    if (heatsink != null)
                    {
                        var hs_asTerminalBlock = heatsink as IMyTerminalBlock;
                        var hs_asFuctionalBlock = hs_asTerminalBlock as IMyFunctionalBlock;

                        if (heatsink is IMyVirtualMass)
                        {
                            //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "Found VMASS");

                            if (hs_asTerminalBlock.BlockDefinition.SubtypeId == "HeatSinkLarge" && hs_asFuctionalBlock.Enabled)
                            {
                                //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "Added Heat Sink and Applied Math");
                                rawThermalOutput -= rawThermalOutput / 20;
                            }

                            else if (hs_asTerminalBlock.BlockDefinition.SubtypeId == "HeatSinkSmall" && hs_asFuctionalBlock.Enabled)
                            {
                                //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "Added Heat Sink and Applied Math");
                                rawThermalOutput -= rawThermalOutput / 40;
                            }
                            //Fuck you Keen.
                            //`if (powerProducer.BlockDefinition.SubtypeId.ToLower().Contains("REEEE"))` doesn't work, use `if (hs_asTerminalBlock.BlockDefinition.SubtypeId == "REEEE")` instead
                        }
                    }
                }
            }

            else if (HeatSinks.Count != 0 && HeatSinks.Count >= 50)
            {
                BroadcastSpamMessage(Beacon);
                SpamMessageSent = false;
            }

            else if (HeatSinks.Count == 0)
            {
                //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "Failed to Add HeatSink");
            }

            double thermalOutput = 0.0d;
            thermalOutput = Math.Round(rawThermalOutput, 4);
            //MyAPIGateway.Utilities.ShowMessage("ThermalBeacon_Debug", "thermalOutput Range: " + thermalOutput.ToString());
            return (float)thermalOutput;
        }
    }
}
