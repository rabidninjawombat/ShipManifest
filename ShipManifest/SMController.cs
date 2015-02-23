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
        internal float sXferAmount = 0f;
        internal bool sXferAmountHasDecimal = false;
        internal bool sXferAmountHasZero = false;
        internal float tXferAmount = 0f;
        internal bool tXferAmountHasDecimal = false;
        internal bool tXferAmountHasZero = false;
        internal float AmtXferred = 0f;

        internal Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        internal bool IsPreLaunch
        {
            get { return Vessel.landedAt == "LaunchPad" || Vessel.landedAt == "Runway"; }
        }

        internal bool CanDrawButton = false;

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
                            if (Settings.EnableCrew && part.CrewCapacity > 0)
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
                    SMAddon.ClearResourceHighlighting(SelectedResourceParts);
                    _prevSelectedResource = _selectedResource;
                    _selectedResource = value;

                    if (value == null)
                        SelectedResourceParts = new List<Part>();
                    else
                        SelectedResourceParts = _partsByResource[_selectedResource];
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Set SelectedResource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                }
            }
        }

        // Provides a list of parts for a given resource.
        // Used to maintain Add/Remove of OnMouseExit handlers
        private List<Part> _selectedResourceParts;
        internal List<Part> SelectedResourceParts
        {
            get
            {
                if (_selectedResourceParts == null)
                    _selectedResourceParts = new List<Part>();
                return _selectedResourceParts;
            }
            set
            {
                SMAddon.ClearResourceHighlighting(_selectedResourceParts);
                _selectedResourceParts = value;
            }
        }

        private Part _selectedPartSource;
        internal Part SelectedPartSource
        {
            get
            {
                try
                {
                    if (_selectedPartSource != null && !FlightGlobals.ActiveVessel.Parts.Contains(_selectedPartSource))
                        _selectedPartSource = null;

                    return _selectedPartSource;
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
                    if (value != _selectedPartSource)
                        SMAddon.ClearPartHighlight(_selectedPartSource);
                    _selectedPartSource = value;
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
                    Utilities.LogMessage(string.Format(" in Set SelectedPartSource.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                }
                //Utilities.LogMessage("Set SelectedPartSource.", "Info", SettingsManager.VerboseLogging);
            }
        }

        private Part _selectedPartTarget;
        internal Part SelectedPartTarget
        {
            get
            {
                try
                {
                    if (_selectedPartTarget != null && !FlightGlobals.ActiveVessel.Parts.Contains(_selectedPartTarget))
                        _selectedPartTarget = null;
                    return _selectedPartTarget;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Get SelectedPartTarget.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                    return null;
                }
            }
            set
            {
                try
                {
                    if (value != _selectedPartTarget)
                        SMAddon.ClearPartHighlight(_selectedPartTarget);
                    _selectedPartTarget = value;
                    if (Settings.EnableCLS)
                        SMAddon.UpdateCLSSpaces();

                    // reset transfer amount (for resource xfer slider control)
                    SMAddon.smController.sXferAmount = 0f;
                    SMAddon.smController.tXferAmount = 0f;
                }
                catch (Exception ex)
                {
                    Utilities.LogMessage(string.Format(" in Set SelectedPartTarget.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
                }
            }
        }

        internal PartModule SelectedModuleSource;
        internal PartModule SelectedModuleTarget;

        internal ICLSPart clsPartSource;
        internal ICLSPart clsPartTarget;
        internal ICLSSpace clsSpaceSource;
        internal ICLSSpace clsSpaceTarget;

        private List<SolarPanel> _solarPanels = new List<SolarPanel>();
        internal List<SolarPanel> SolarPanels
        {
            get
            {
                if (_solarPanels == null)
                    _solarPanels = new List<SolarPanel>();
                return _solarPanels;
            }
            set
            {
                _solarPanels.Clear();
                _solarPanels = value;
            }
        }

        internal GameEvents.FromToAction<Part, Part> evaAction;

        #endregion

        internal SMController()
        {
        }

        #region Action Methods

        private void AddCrew(int count, Part part)
        {
            if (IsPreLaunch && !PartCrewIsFull(part))
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

        internal static void AddCrew(ProtoCrewMember kerbal, Part part)
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

        internal static bool PartCrewIsFull(Part part)
        {
            return !(part.protoModuleCrew.Count < part.CrewCapacity);
        }

        internal Part FindPart(ProtoCrewMember kerbal)
        {
            foreach (Part part in FlightGlobals.ActiveVessel.Parts)
            {
                foreach (ProtoCrewMember curkerbal in part.protoModuleCrew)
                {
                    if (curkerbal == kerbal)
                    {
                        return part;
                    }
                }
            }
            return null;
        }

        internal static void RemoveCrew(ProtoCrewMember member, Part part)
        {
            part.RemoveCrewmember(member);
            member.rosterStatus = ProtoCrewMember.RosterStatus.Available;
        }

        internal static void RespawnKerbal(ProtoCrewMember kerbal)
        {
            kerbal.SetTimeForRespawn(0);
            kerbal.Spawn();
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
                AddCrew(part.CrewCapacity - part.protoModuleCrew.Count, part);
            }
            SMAddon.FireEventTriggers();
        }

        internal void EmptyVesselCrew()
        {
            foreach (var part in _partsByResource["Crew"])
            {
                for (int i = part.protoModuleCrew.Count - 1; i >= 0; i--)
                {
                    RemoveCrew(part.protoModuleCrew[i], part);
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
                            SolarPanel pPanel = new SolarPanel();
                            pPanel.PanelModule = pModule;
                            _solarPanels.Add(pPanel);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.LogMessage(string.Format("Error in GetSolarPanels().\r\nError:  {0}", ex.ToString()), "Error", true);
            }
        }

        #endregion

    }
}
