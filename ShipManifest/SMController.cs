using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;
using ConnectedLivingSpace;

namespace ShipManifest
{
    internal class SMController
    {
        #region Singleton stuff

        private static Dictionary<WeakReference<Vessel>, SMController> controllers = new Dictionary<WeakReference<Vessel>, SMController>();

        internal static SMController GetInstance(Vessel vessel)
        {
            foreach (var kvp in controllers.ToArray())
            {
                var wr = kvp.Key;
                var v = wr.Target;
                if (v == null)
                {
                    controllers.Remove(wr);
                }
                else if (v == vessel)
                {
                    return controllers[wr];
                }
            }

            var commander = new SMController();
            controllers[new WeakReference<Vessel>(vessel)] = commander;
            return commander;
        }

        #endregion

        #region Properties

        // variables used for moving resources.
        internal double sXferAmount = 0;
        internal bool sXferAmountHasDecimal = false;
        internal bool sXferAmountHasZero = false;
        internal double tXferAmount = 0;
        internal bool tXferAmountHasDecimal = false;
        internal bool tXferAmountHasZero = false;
        internal double AmtXferred = 0;

        internal Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        internal bool IsPreLaunch
        {
            get { return Vessel.landedAt == "LaunchPad" || Vessel.landedAt == "Runway"; }
        }


        #endregion

        #region Datasource properties

        // dataSource for Resource manifest and ResourceTransfer windows
        // Provides a list of resources and the parts that contain that resource.
        internal List<string> ResourceList = new List<string>();
        internal Dictionary<string, List<Part>> _partsByResource = null;
        internal Dictionary<string, List<Part>> PartsByResource
        {
            get
            {
                try
                {
                    if (_partsByResource == null)
                        _partsByResource = new Dictionary<string, List<Part>>();
                    else
                        _partsByResource.Clear();

                    // Let's update...
                    if (FlightGlobals.ActiveVessel != null)
                    {
                        //Utilities.LogMessage(string.Format(" getting partsbyresource.  "), "Info", SettingsManager.VerboseLogging);
                        SMAddon.vessel = Vessel;

                        _partsByResource = new Dictionary<string, List<Part>>();
                        foreach (Part part in Vessel.Parts)
                        {
                            // First let's Get any Crew, if desired...
                            if (Settings.EnableCrew && (part.CrewCapacity > 0 && part.partInfo.name != "kerbalEVA"))
                            {
                                bool vResourceFound = false;
                                // is resource in the list yet?.
                                if (_partsByResource.Keys.Contains("Crew"))
                                {
                                    // found resource.  lets add part to its list.
                                    vResourceFound = true;
                                    List<Part> eParts = _partsByResource["Crew"];
                                    eParts.Add(part);
                                }
                                if (!vResourceFound)
                                {
                                    // found a new resource.  lets add it to the list of resources.
                                    List<Part> nParts = new List<Part>();
                                    nParts.Add(part);
                                    _partsByResource.Add("Crew", nParts);
                                }
                            }
                            // Let's Get any Science...
                            if (Settings.EnableScience)
                            {
                                bool mResourceFound = false;
                                IScienceDataContainer[] sciModules = part.FindModulesImplementing<IScienceDataContainer>().ToArray();
                                foreach (IScienceDataContainer pm in sciModules)
                                {
                                    // is resource in the list yet?.
                                    // 
                                    if (!mResourceFound && (pm is IScienceDataContainer))
                                    {
                                        if (_partsByResource.Keys.Contains("Science"))
                                        {
                                            mResourceFound = true;
                                            List<Part> eParts = _partsByResource["Science"];
                                            eParts.Add(part);
                                            break;
                                        }
                                        if (!mResourceFound)
                                        {
                                            // found a new resource.  lets add it to the list of resources.
                                            List<Part> nParts = new List<Part>();
                                            nParts.Add(part);
                                            _partsByResource.Add("Science", nParts);
                                            mResourceFound = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            // Now, let's get flight Resources.
                            foreach (PartResource resource in part.Resources)
                            {
                                // Realism Mode.  we want to exclude Resources with TransferMode = NONE...
                                if (!Settings.RealismMode || (Settings.RealismMode && resource.info.resourceTransferMode != ResourceTransferMode.NONE))
                                {
                                    bool vResourceFound = false;
                                    // is resource in the list yet?.
                                    if (_partsByResource.Keys.Contains(resource.info.name))
                                    {
                                        vResourceFound = true;
                                        List<Part> eParts = _partsByResource[resource.info.name];
                                        eParts.Add(part);
                                    }
                                    if (!vResourceFound)
                                    {
                                        // found a new resource.  lets add it to the list of resources.
                                        List<Part> nParts = new List<Part>();
                                        nParts.Add(part);
                                        _partsByResource.Add(resource.info.name, nParts);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" getting partsbyresource.  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                    _partsByResource = null;
                }

                if (_partsByResource != null)
                    ResourceList = new List<string>(_partsByResource.Keys);
                else
                    ResourceList = null;

                return _partsByResource;
            }
        }

        // dataSource for Resource manifest and ResourceTransfer windows
        // Holds the Resource.info.name selected in the Resource Manifest Window.
        private string _prevSelectedResource;
        private string _selectedResource;
        internal string SelectedResource
        {
            get
            {
                return _selectedResource;
            }
            set
            {
                try
                {
                    if (_selectedResource != null && _selectedResource != "")
                        SMAddon.ClearResourceHighlighting(_partsByResource[_selectedResource]);
                    _prevSelectedResource = _selectedResource;
                    _selectedResource = value;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Set SelectedResource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                }
            }
        }

        // Multi-Part Xfer Storage
        internal Dictionary<List<string>, List<Part>> _selectedResourcesParts = null;
        internal Dictionary<List<string>, List<Part>> SelectedResourcesParts
        {
            get
            {
                return _selectedResourcesParts;
            }
            set
            {
                _selectedResourcesParts = value;
            }
        }

        private List<Part> _selectedPartsSource = new List<Part>();
        internal List<Part> SelectedPartsSource
        {
            get
            {
                try
                {
                    if (_selectedPartsSource == null)
                        _selectedPartsSource = new List<Part>();
                    foreach (Part part in _selectedPartsSource)
                    {
                        if (!FlightGlobals.ActiveVessel.Parts.Contains(part))
                        {
                            _selectedPartsSource = new List<Part>();
                            break;
                        }
                    }
                    return _selectedPartsSource;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Get SelectedPartSource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                    return null;
                }
            }
            set
            {
                try
                {
                    if (value != _selectedPartsSource)
                        SMAddon.ClearPartsHighlight(_selectedPartsSource);
                    _selectedPartsSource = value;
                    if (Settings.EnableCLS)
                    {
                        SMAddon.UpdateCLSSpaces();
                    }

                    // reset transfer amount (for resource xfer slider control)
                    SMAddon.smController.sXferAmount = 0;
                    SMAddon.smController.tXferAmount = 0;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Set SelectedPartsSource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                }
                //Utilities.LogMessage("Set SelectedPartsSource.", "Info", SettingsManager.VerboseLogging);
            }
        }

        private List<Part> _selectedPartsTarget = new List<Part>();
        internal List<Part> SelectedPartsTarget
        {
            get
            {
                try
                {
                    if (_selectedPartsTarget == null)
                        _selectedPartsTarget = new List<Part>();
                    foreach (Part part in _selectedPartsTarget)
                    {
                        if (!FlightGlobals.ActiveVessel.Parts.Contains(part))
                        {
                            _selectedPartsTarget = new List<Part>();
                            break;
                        }
                    }
                    return _selectedPartsTarget;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Get SelectedPartsTarget.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                    return null;
                }
            }
            set
            {
                try
                {
                    if (value != _selectedPartsTarget)
                        SMAddon.ClearPartsHighlight(_selectedPartsTarget);
                    _selectedPartsTarget = value;
                    if (Settings.EnableCLS)
                        SMAddon.UpdateCLSSpaces();

                    // reset transfer amount (for resource xfer slider control)
                    SMAddon.smController.sXferAmount = 0;
                    SMAddon.smController.tXferAmount = 0;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Set SelectedPartsTarget.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                }
            }
        }

        // Single Part Xfer Storage
        //private Part _selectedPartSource;
        //internal Part SelectedPartSource
        //{
        //    get
        //    {
        //        try
        //        {
        //            if (_selectedPartSource != null && !FlightGlobals.ActiveVessel.Parts.Contains(_selectedPartSource))
        //                _selectedPartSource = null;

        //            return _selectedPartSource;
        //        }
        //        catch (Exception ex)
        //        {
        //            Utilities.LogMessage(string.Format(" in Get SelectedPartSource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
        //            return null;
        //        }
        //    }
        //    set
        //    {
        //        try
        //        {
        //            if (value != _selectedPartSource)
        //                SMAddon.ClearPartHighlight(_selectedPartSource);
        //            _selectedPartSource = value;
        //            if (Settings.EnableCLS)
        //            {
        //                SMAddon.UpdateCLSSpaces();
        //            }

        //            // reset transfer amount (for resource xfer slider control)
        //            SMAddon.smController.sXferAmount = 0;
        //            SMAddon.smController.tXferAmount = 0;
        //            if (_selectedResource == "Crew")
        //            {

        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Utilities.LogMessage(string.Format(" in Set SelectedPartSource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
        //        }
        //        //Utilities.LogMessage("Set SelectedPartSource.", "Info", SettingsManager.VerboseLogging);
        //    }
        //}

        //private Part _selectedPartTarget;
        //internal Part SelectedPartTarget
        //{
        //    get
        //    {
        //        try
        //        {
        //            if (_selectedPartTarget != null && !FlightGlobals.ActiveVessel.Parts.Contains(_selectedPartTarget))
        //                _selectedPartTarget = null;
        //            return _selectedPartTarget;
        //        }
        //        catch (Exception ex)
        //        {
        //            Utilities.LogMessage(string.Format(" in Get SelectedPartTarget.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
        //            return null;
        //        }
        //    }
        //    set
        //    {
        //        try
        //        {
        //            if (value != _selectedPartTarget)
        //                SMAddon.ClearPartHighlight(_selectedPartTarget);
        //            _selectedPartTarget = value;
        //            if (Settings.EnableCLS)
        //                SMAddon.UpdateCLSSpaces();

        //            // reset transfer amount (for resource xfer slider control)
        //            SMAddon.smController.sXferAmount = 0;
        //            SMAddon.smController.tXferAmount = 0;
        //        }
        //        catch (Exception ex)
        //        {
        //            Utilities.LogMessage(string.Format(" in Set SelectedPartTarget.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
        //        }
        //    }
        //}

        // Stock Crew transfer part storage

        internal Part CrewXferSource;
        internal Part CrewXferTarget;
        internal ProtoCrewMember CrewXferMember;

        internal PartModule SelectedModuleSource;
        internal PartModule SelectedModuleTarget;

        internal ICLSPart clsPartSource;
        internal ICLSPart clsPartTarget;
        internal ICLSSpace clsSpaceSource;
        internal ICLSSpace clsSpaceTarget;

        // Control Window parts
        private List<ModHatch> _hatches = new List<ModHatch>();
        internal List<ModHatch> Hatches
        {
            get
            {
                if (_hatches == null)
                    _hatches = new List<ModHatch>();
                return _hatches;
            }
            set
            {
                _hatches.Clear();
                _hatches = value;
            }
        }

        private List<ModSolarPanel> _solarPanels = new List<ModSolarPanel>();
        internal List<ModSolarPanel> SolarPanels
        {
            get
            {
                if (_solarPanels == null)
                    _solarPanels = new List<ModSolarPanel>();
                return _solarPanels;
            }
            set
            {
                _solarPanels.Clear();
                _solarPanels = value;
            }
        }

        private List<ModLight> _lights = new List<ModLight>();
        internal List<ModLight> Lights
        {
            get
            {
                if (_lights == null)
                    _lights = new List<ModLight>();
                return _lights;
            }
            set
            {
                _lights.Clear();
                _lights = value;
            }
        }

        private List<ModAntenna> _antennas = new List<ModAntenna>();
        internal List<ModAntenna> Antennas
        {
            get
            {
                if (_antennas == null)
                    _antennas = new List<ModAntenna>();
                return _antennas;
            }
            set
            {
                _antennas.Clear();
                _antennas = value;
            }
        }

        //internal GameEvents.FromToAction<Part, Part> evaAction;

        #endregion

        internal SMController()
        {
        }

        #region Action Methods

        private void FillPartCrew(int count, Part part)
        {
            if (IsPreLaunch && !CrewPartIsFull(part))
            {
                for (int i = 0; i < part.CrewCapacity && i < count; i++)
                {
                    ProtoCrewMember kerbal = HighLogic.CurrentGame.CrewRoster.GetNextOrNewKerbal();
                    part.AddCrewmember(kerbal);

                    if (kerbal.seat != null)
                        kerbal.seat.SpawnCrew();
                }
            }
        }

        internal static void AddCrewMember(ProtoCrewMember kerbal, Part part)
        {
            part.AddCrewmember(kerbal);
            kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
            if (part.internalModel != null)
            {
                if (kerbal.seat != null)
                    kerbal.seat.SpawnCrew();
            }
            SMAddon.FireEventTriggers();
        }

        internal static void RemoveCrewMember(ProtoCrewMember member, Part part)
        {
            part.RemoveCrewmember(member);
            member.rosterStatus = ProtoCrewMember.RosterStatus.Available;
        }

        internal static bool CrewPartIsFull(Part part)
        {
            return !(part.protoModuleCrew.Count < part.CrewCapacity);
        }

        internal Part FindKerbalPart(ProtoCrewMember pKerbal)
        {
            Part kPart = FlightGlobals.ActiveVessel.Parts.Find(x => x.protoModuleCrew.Find(y => y == pKerbal) != null);
            return kPart;
        }

        internal static void RespawnKerbal(ProtoCrewMember kerbal)
        {
            kerbal.SetTimeForRespawn(0);
            // This call causes issues in KSC scene, and is not needed.
            //kerbal.Spawn();
            kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Available;
            HighLogic.CurrentGame.CrewRoster.GetNextAvailableKerbal();
        }

        internal void RespawnCrew()
        {
            this.Vessel.SpawnCrew();
            SMAddon.FireEventTriggers();
        }

        internal void FillVesselCrew()
        {
            foreach (var part in _partsByResource["Crew"])
            {
                FillPartCrew(part.CrewCapacity - part.protoModuleCrew.Count, part);
            }
            SMAddon.FireEventTriggers();
        }

        internal void EmptyVesselCrew()
        {
            foreach (var part in _partsByResource["Crew"])
            {
                for (int i = part.protoModuleCrew.Count - 1; i >= 0; i--)
                {
                    RemoveCrewMember(part.protoModuleCrew[i], part);
                }
                SMAddon.FireEventTriggers();
            }
        }

        internal void FillVesselResources()
        {
            List<string> resources = _partsByResource.Keys.ToList<string>();
            foreach (string resourceName in resources)
            {
                if (resourceName != "Crew" && resourceName != "Science")
                {
                    foreach (Part part in _partsByResource[resourceName])
                    {
                        foreach (PartResource resource in part.Resources)
                        {
                            if (resource.info.name == resourceName)
                                resource.amount = resource.maxAmount;
                        }
                    }
                }
            }
        }

        internal void EmptyVesselResources()
        {
            List<string> resources = _partsByResource.Keys.ToList<string>();
            foreach (string resourceName in resources)
            {
                if (resourceName != "Crew" && resourceName != "Science")
                {
                    foreach (Part part in _partsByResource[resourceName])
                    {
                        foreach (PartResource resource in part.Resources)
                        {
                            if (resource.info.name == resourceName)
                                resource.amount = 0;
                        }
                    }
                }
            }
        }

        internal void DumpResource(string resourceName)
        {
            foreach (Part part in _partsByResource[resourceName])
            {
                foreach (PartResource resource in part.Resources)
                {
                    if (resource.info.name == resourceName)
                    {
                        resource.amount = 0;
                    }
                }
            }
        }

        internal void FillResource(string resourceName)
        {
            foreach (Part part in _partsByResource[resourceName])
            {
                foreach (PartResource resource in part.Resources)
                {
                    if (resource.info.name == resourceName)
                    {
                        resource.amount = resource.maxAmount;
                    }
                }
            }
        }

        internal static void DumpPartResource(Part part, string resourceName)
        {
            foreach (PartResource resource in part.Resources)
            {
                if (resource.info.name == resourceName)
                {
                    resource.amount = 0;
                }
            }
        }

        internal static void FillPartResource(Part part, string resourceName)
        {
            foreach (PartResource resource in part.Resources)
            {
                if (resource.info.name == resourceName)
                {
                    resource.amount = resource.maxAmount;
                }
            }
        }

        internal void GetHatches()
        {
            _hatches.Clear();
            try
            {
                foreach (ICLSPart iPart in SMAddon.clsAddon.Vessel.Parts)
                {
                    foreach (PartModule pModule in iPart.Part.Modules)
                    {
                        if (pModule.moduleName == "ModuleDockingHatch")
                        {
                            ModHatch pHatch = new ModHatch();
                            pHatch.HatchModule = pModule;
                            pHatch.CLSPart = iPart;
                            _hatches.Add(pHatch);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.LogMessage(string.Format("Error in GetHatches().\r\nError:  {0}", ex.ToString()), "Error", true);
            }
        }

        internal void GetSolarPanels()
        {
            _solarPanels.Clear();
            try
            {
                foreach (Part pPart in SMAddon.vessel.Parts)
                {
                    foreach (PartModule pModule in pPart.Modules)
                    {
                        if (pModule.moduleName == "ModuleDeployableSolarPanel")
                        {
                            ModuleDeployableSolarPanel iModule = (ModuleDeployableSolarPanel)pModule;
                            if (iModule.Events["Extend"].active || iModule.Events["Retract"].active)
                            {
                                ModSolarPanel pPanel = new ModSolarPanel();
                                pPanel.PanelModule = pModule;
                                pPanel.SPart = pPart;
                                _solarPanels.Add(pPanel);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.LogMessage(string.Format("Error in GetSolarPanels().\r\nError:  {0}", ex.ToString()), "Error", true);
            }
        }

        internal void GetAntennas()
        {
            _antennas.Clear();
            try
            {
                // Added support for RemoteTech antennas
                foreach (Part pPart in SMAddon.vessel.Parts)
                {
                    if (pPart.Modules.Contains("ModuleDataTransmitter") || pPart.Modules.Contains("ModuleRTAntenna"))
                    {
                        ModAntenna pAntenna = new ModAntenna();
                        pAntenna.SPart = pPart;
                        foreach (PartModule pModule in pPart.Modules)
                        {
                            if (pModule.moduleName == "ModuleDataTransmitter" || pModule.moduleName == "ModuleRTAntenna")
                            {
                                pAntenna.XmitterModule = pModule;
                            }
                            if (pModule.moduleName == "ModuleAnimateGeneric" && (pModule.Events["Toggle"].guiName == "Extend" || pModule.Events["Toggle"].guiName == "Retract"))
                            {
                                pAntenna.AnimateModule = pModule;
                            }
                        }
                        _antennas.Add(pAntenna);
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.LogMessage(string.Format("Error in GetAntennas().\r\nError:  {0}", ex.ToString()), "Error", true);
            }
        }

        internal void GetLights()
        {
            _lights.Clear();
            try
            {
                foreach (Part pPart in SMAddon.vessel.Parts)
                {
                    foreach (PartModule pModule in pPart.Modules)
                    {
                        if (pModule.moduleName == "ModuleLight")
                        {
                            ModLight pLight = new ModLight();
                            pLight.LightModule = pModule;
                            pLight.SPart = pPart;
                            _lights.Add(pLight);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.LogMessage(string.Format("Error in GetLights().\r\nError:  {0}", ex.ToString()), "Error", true);
            }
        }

        #endregion

    }
}
