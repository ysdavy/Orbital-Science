﻿#region license
/* DMagic Orbital Science - Asteroid Scanner
 * Science Module For Asteroid Scanning Experiment
 *
 * Copyright (c) 2014, David Grandy <david.grandy@gmail.com>
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, 
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice, 
 * this list of conditions and the following disclaimer in the documentation and/or other materials 
 * provided with the distribution.
 * 
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used 
 * to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DMagic.Scenario;
using KSP.UI.Screens.Flight.Dialogs;
using KSP.Localization;

namespace DMagic.Part_Modules
{
	class DMAsteroidScanner : PartModule, IScienceDataContainer
	{

		#region Fields

		[KSPField]
		public string experimentID = "";
		[KSPField]
		public string animationName = "";
		[KSPField]
		public string greenLight = "";
		[KSPField]
		public string yellowLight = "";
		[KSPField]
		public string USBayAnimation = "";
		[KSPField]
		public bool USScience = false;
        [KSPField]
        public bool USTwoScience = false;
        [KSPField]
        public string RaySourceTransform = String.Empty;
		[KSPField]
		public bool rerunnable = true;
		[KSPField]
		public bool dataIsCollectable = true;
		[KSPField]
		public float transmitValue = 1f;
		[KSPField(isPersistant=true)]
		public bool IsDeployed = false;
		[KSPField(isPersistant=true)]
		public bool Inoperable = false;
		[KSPField(isPersistant = true)]
		public bool Deployed = false;
		[KSPField(guiActive = true, guiName = "Status")]
		public string status;
		[KSPField]
		public int usageReqMaskExternal = -1;
		[KSPField]
		public string usageReqMessage = "";
		[KSPField(isPersistant = true)]
		public bool showLines = true;

		private string failMessage = "";
		private string asteroidBodyNameFixed = "Eeloo";
		private const string baseTransformName = "DishBaseTransform";
		private const string transformName = "DishTransform";
		private const string transformRotatorName = "DishArmTransform";
		private Animation Anim;
		private Animation IndicatorAnim1;
		private Animation IndicatorAnim2;
		private Animation USAnim;
		internal ScienceExperiment exp = null;
		private List<ScienceData> scienceReports = new List<ScienceData>();
		private bool receiverInRange = false;
		private bool asteroidInSight = false;
		private bool rotating = false;
		private bool resourceOn = false;
		private bool fullyDeployed = false;
		private DMAsteroidScanner targetModule = null;
		private Transform dishBase;
		private Transform dish;
		private Transform dishArm;
        private Transform _raySource;
        private float targetDistance = 0f;
		private float[] astWidth = new float[6] { 4, 8, 14, 20, 26, 40 };
		private ExperimentsResultDialog resultsDialog;
		private bool hasContainer;

        private Shader lineShader;
        private Material lineMaterial;

        private string resError;
        private bool useResources;

        #endregion

        #region PartModule

        public override void OnAwake()
		{
			GameEvents.onGamePause.Add(onPause);
			GameEvents.onGameUnpause.Add(onUnPause);
			GameEvents.onVesselStandardModification.Add(onVesselModified);
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (!string.IsNullOrEmpty(animationName))
				Anim = part.FindModelAnimators(animationName)[0];

			if (!string.IsNullOrEmpty(greenLight))
				IndicatorAnim1 = part.FindModelAnimators(greenLight)[0];

			if (!string.IsNullOrEmpty(yellowLight))
				IndicatorAnim2 = part.FindModelAnimators(yellowLight)[0];

			if (USScience && !string.IsNullOrEmpty(USBayAnimation))
				USAnim = part.FindModelAnimators(USBayAnimation)[0];

            if (USTwoScience && !string.IsNullOrEmpty(RaySourceTransform))
                _raySource = part.FindModelTransform(RaySourceTransform);

            if (!string.IsNullOrEmpty(experimentID))
				exp = ResearchAndDevelopment.GetExperiment(experimentID);

			if (IsDeployed)
			{
				fullyDeployed = true;
				animator(0f, 1f, Anim, animationName);

				if (USScience)
					animator(0f, 1f, USAnim, USBayAnimation);
			}

			dishBase = part.FindModelTransform(baseTransformName);
			dish = part.FindModelTransform(transformName);
			dishArm = part.FindModelTransform(transformRotatorName);

            if (resHandler != null && resHandler.inputResources != null && resHandler.inputResources.Count > 0 && resHandler.inputResources[0].rate > 0)
                useResources = true;
            else
                useResources = false;

            if (FlightGlobals.Bodies.Count >= 17)
				asteroidBodyNameFixed = FlightGlobals.Bodies[16].bodyName;

            if (!HighLogic.LoadedSceneIsEditor)
			    findContainers();
		}

		public override void OnSave(ConfigNode node)
		{
			node.RemoveNodes("ScienceData");
			foreach (ScienceData storedData in scienceReports)
			{
				ConfigNode storedDataNode = node.AddNode("ScienceData");
				storedData.Save(storedDataNode);
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			if (node.HasNode("ScienceData"))
			{
				foreach (ConfigNode storedDataNode in node.GetNodes("ScienceData"))
				{
					ScienceData data = new ScienceData(storedDataNode);
					scienceReports.Add(data);
				}
			}
		}

		private void OnDestroy()
		{
			GameEvents.onGamePause.Remove(onPause);
			GameEvents.onGameUnpause.Remove(onUnPause);
			GameEvents.onVesselStandardModification.Remove(onVesselModified);
		}

		private void onPause()
		{
			if (resultsDialog != null)
				resultsDialog.gameObject.SetActive(false);
		}

		private void onUnPause()
		{
			if (resultsDialog != null)
				resultsDialog.gameObject.SetActive(true);
		}

		private void onVesselModified(Vessel v)
		{
			if (v == vessel && HighLogic.LoadedSceneIsFlight)
				findContainers();
		}

		private void findContainers()
		{
			for (int i = vessel.Parts.Count - 1; i >= 0; i--)
			{
				Part p = vessel.Parts[i];

				if (p == null)
					continue;

				if (p.State == PartStates.DEAD)
					continue;

				ModuleScienceContainer container = p.FindModuleImplementing<ModuleScienceContainer>();

				if (container == null)
					continue;

				hasContainer = container.canBeTransferredToInVessel;
				break;
			}
		}

		private void Update()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				EventsCheck();
				string s = "Deactivated";
				if (fullyDeployed && resourceOn)
				{
					s = "Searching...";
					Vessel target = FlightGlobals.fetch.VesselTarget as Vessel;
					FlightCoMTracker targetObj = FlightGlobals.fetch.VesselTarget as FlightCoMTracker;

					if (target != null)
					{
						targetModule = target.FindPartModulesImplementing<DMAsteroidScanner>().FirstOrDefault(t => t.fullyDeployed && t.resourceOn);
					}
					else if (targetObj != null)
					{
						var targets = targetObj.GetOrbitDriver().vessel.FindPartModulesImplementing<DMAsteroidScanner>();
						if (targets.Count > 0)
						{
							if (targets.Count > 1)
							{
								foreach (DMAsteroidScanner t in targets)
								{
									if (t == this)
										continue;

									if (!(t.fullyDeployed && t.resourceOn))
										continue;

									float d = (t.part.transform.position - dishBase.position).sqrMagnitude;
									if (d > 4f)
									{
										targetModule = t;
										break;
									}
									else
									{
										targetModule = null;
										targetDistance = 0f;
										receiverInRange = asteroidInSight = false;
									}
								}
							}
							else if (targets[0] != this)
							{
								if (targets[0].fullyDeployed && targets[0].resourceOn)
									targetModule = targets[0];
							}
						}
						else
						{
							targetModule = null;
							targetDistance = 0f;
							receiverInRange = asteroidInSight = false;
						}
					}
					else
					{
						var targets = vessel.FindPartModulesImplementing<DMAsteroidScanner>();
						if (targets.Count > 1)
						{
							foreach (DMAsteroidScanner t in targets)
							{
								if (t == this)
									continue;

								if (!(t.fullyDeployed && t.resourceOn))
									continue;

								float d = (t.part.transform.position - dishBase.position).sqrMagnitude;
								if (d > 4f)
								{
									targetModule = t;
									break;
								}
								else
								{
									targetDistance = 0f;
									targetModule = null;
									receiverInRange = asteroidInSight = false;
								}
							}
						}
						else
						{
							targetDistance = 0f;
							targetModule = null;
							receiverInRange = asteroidInSight = false;
						}
					}

					if (targetModule != null)
					{
						s = "Receiver Located";
						targetDistance = (targetModule.part.transform.position - dishBase.position).magnitude;
						if (targetDistance < 2000 && targetDistance > 2)
						{
							s = "Receiver In Range";
							receiverInRange = true;
							if (simpleRayHit())
							{
								s = "Experiment Ready";
								asteroidInSight = true;
							}
							else
								asteroidInSight = false;
						}
						else
						{
							receiverInRange = asteroidInSight = false;
							targetDistance = 0f;
						}
					}
					else
					{
						receiverInRange = asteroidInSight = false;
						targetDistance = 0f;
					}

					if (receiverInRange && targetModule != null)
						lookAtTarget();
					else
						searchForTarget();
					rotating = true;
				}
				else if (fullyDeployed && !resourceOn && useResources)
				{
					s = "No Power";
					targetDistance = 0f;
					targetModule = null;
					receiverInRange = asteroidInSight = false;
				}
				else if (rotating)
				{
					spinDownDish();
					targetDistance = 0f;
					targetModule = null;
					receiverInRange = asteroidInSight = false;
				}
				else
				{
					targetDistance = 0f;
					targetModule = null;
					receiverInRange = asteroidInSight = false;
				}

				lightSwitch(receiverInRange && asteroidInSight, !IsDeployed);

				status = s;
			}
		}

		private void EventsCheck()
		{
			Events["ResetExperiment"].active = scienceReports.Count > 0;
			Events["CollectDataExternalEvent"].active = scienceReports.Count > 0 && dataIsCollectable;
			Events["DeployExperiment"].active = scienceReports.Count == 0 && !Deployed && !Inoperable && asteroidInSight;
			Events["ReviewDataEvent"].active = scienceReports.Count > 0;
			Events["toggleLine"].active = fullyDeployed;
			Events["TransferDataEvent"].active = hasContainer && dataIsCollectable && scienceReports.Count > 0;
		}

		//Point the dish at the target
		private void lookAtTarget()
		{
			//A line from the dish transform to the target dish's position
			Vector3 targetPos = dishBase.InverseTransformPoint(targetModule.part.transform.position);

			//Use some simple trig to convert the Vector3 into two angle components
			float angleZ = Mathf.Atan2(targetPos.y, targetPos.x);
			float angleY = Mathf.Atan2(-targetPos.z, new Vector2(targetPos.x, targetPos.y).magnitude);

			angleZ *= Mathf.Rad2Deg;
			angleY *= Mathf.Rad2Deg;

			//Normalize the resulting angle to make sure it is within 0-360; offset the Y angle by 90 to compensate
			//for the initial transform rotation
			angleZ = normalizeAngle(angleZ);
			angleY = normalizeAngle(angleY + 90);

			//These two methods rotate the dish base and the dish to point at the target location
			//Quaternions are generated by rotating about a certain axis by the amount calculated above
			dishArm.localRotation = Quaternion.RotateTowards(dishArm.localRotation, Quaternion.AngleAxis(angleZ, Vector3.forward), Time.deltaTime * 30f);
			dish.localRotation = Quaternion.RotateTowards(dish.localRotation, Quaternion.AngleAxis(angleY, Vector3.up), Time.deltaTime * 30f);

			if (showLines)
				drawLine(dishBase.position, targetModule.part.transform.position, Color.red);
		}

		private void drawLine(Vector3 start, Vector3 end, Color c)
		{
			GameObject line = new GameObject();
			line.transform.position = start;
            LineRenderer lr = line.AddComponent<LineRenderer>();

            if (lineShader == null)
                lineShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

            if (lineMaterial == null)
                lineMaterial = new Material(lineShader);

            lr.material = lineMaterial;

            lr.startColor = c;
            lr.endColor = c * 0.3f;

            lr.startWidth = 0.1f;
            lr.endWidth = 0.05f;

            //lr.SetColors(c, c * 0.3f);
            //lr.SetWidth(0.1f, 0.05f);

			lr.SetPosition(0, start);
			lr.SetPosition(1, end);
			Destroy(line, TimeWarp.deltaTime);
		}

		private void searchForTarget()
		{
			//Slowly rotate dish
			dishArm.Rotate(Vector3.forward * Time.deltaTime * 20f);
			if (dish.localEulerAngles.y < 44 || dish.localEulerAngles.y > 46)
				dish.localRotation = Quaternion.RotateTowards(dish.localRotation, Quaternion.AngleAxis(45, Vector3.up), Time.deltaTime * 20f);
		}

		private void spinDownDish()
		{
			//Spin the dish back to its starting position
			if (dishArm.localEulerAngles.z > 1 || dish.localEulerAngles.y > 1)
			{
				if (dishArm.localEulerAngles.z > 1)
					dishArm.Rotate(Vector3.forward * Time.deltaTime * 20f);
				if (dish.localEulerAngles.y > 1)
					dish.localRotation = Quaternion.RotateTowards(dish.localRotation, Quaternion.AngleAxis(0, Vector3.up), Time.deltaTime * 20f);
			}
			else
				rotating = false;
		}

		private float normalizeAngle(float a)
		{
			a = a % 360;
			if (a < 0)
				a += 360;
			return a;
		}

		private bool simpleRayHit()
		{
			Vector3 tPos = dish.position;
			Ray r = new Ray(tPos, dish.forward);
			RaycastHit hit = new RaycastHit();

				Physics.Raycast(r, out hit, 2000, 1 << 0);
				if (hit.collider != null)
				{
					if (hit.collider.attachedRigidbody != null)
					{
						Part a = Part.FromGO(hit.transform.gameObject) ?? hit.transform.gameObject.GetComponentInParent<Part>();

						if (a != null)
						{
							if (a.Modules.Contains("ModuleAsteroid"))
								return true;
						}
					}
				}

			return false;
		}

		public override string GetInfo()
		{
			string info = base.GetInfo();
			info += string.Format("Transmission: {0:P0}\n", transmitValue);
            if (resHandler != null && resHandler.inputResources != null && resHandler.inputResources.Count > 0 && resHandler.inputResources[0].rate > 0)
            {
				info += string.Format("Requires:\n-{0}: {1:F1}/s\n", Localizer.Format(resHandler.inputResources[0].title), resHandler.inputResources[0].rate);
			}
			return info;
		}

		private void FixedUpdate()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				if (useResources)
				{
                    if (IsDeployed)
                    {
                        if (resHandler.UpdateModuleResourceInputs(ref resError, 1, 0.9, true, false))
                            resourceOn = true;
                        else
                        {
                            if (!fullyDeployed)
                                StopCoroutine("deployEvent");

                            StartCoroutine("retractEvent");

                            ScreenMessages.PostScreenMessage("Not enough " + Localizer.Format(resHandler.inputResources[0].title) + ", shutting down experiment", 4f, ScreenMessageStyle.UPPER_CENTER);

                            resourceOn = false;
                        }
                    }
				}
				else
					resourceOn = true;
			}
		}

		#endregion

		#region Animator

		//Controls the main, door-opening animation
		private void animator(float speed, float time, Animation a, string name)
		{
			if (a != null)
			{
				a[name].speed = speed;
				if (!a.IsPlaying(name))
				{
					a[name].normalizedTime = time;
					a.Blend(name, 1f);
				}
			}
		}

		private void lightAnimator(Animation a, string name, bool stop)
		{
			if (!a.IsPlaying(name) && !stop)
			{
				a[name].speed = 1f;
				a[name].normalizedTime = 0f;
				a[name].wrapMode = WrapMode.Loop;
				a.Blend(name);
			}
			else if (stop)
			{
				a[name].normalizedTime = a[name].normalizedTime % 1;
				a[name].wrapMode = WrapMode.Clamp;
			}
		}

		//Cludgy method for controlling the indicator lights...
		private void lightSwitch(bool On, bool AllOff)
		{
			if (IndicatorAnim1 != null & IndicatorAnim2 != null)
			{
				if (AllOff)
				{
					if (IndicatorAnim1.IsPlaying(greenLight))
						lightAnimator(IndicatorAnim1, greenLight, true);
					if (IndicatorAnim2.IsPlaying(yellowLight))
						lightAnimator(IndicatorAnim2, yellowLight, true);
				}
				else if (On)
				{
					if (!IndicatorAnim1.IsPlaying(greenLight))
						lightAnimator(IndicatorAnim1, greenLight, false);
					if (IndicatorAnim2.IsPlaying(yellowLight))
						lightAnimator(IndicatorAnim2, yellowLight, true);
				}
				else
				{
					if (IndicatorAnim1.IsPlaying(greenLight))
						lightAnimator(IndicatorAnim1, greenLight, true);
					if (!IndicatorAnim2.IsPlaying(yellowLight))
						lightAnimator(IndicatorAnim2, yellowLight, false);
				}
			}
		}

		private IEnumerator deployEvent()
		{
            if (USTwoScience)
            {
                RaycastHit hit;
                
                if (_raySource != null && Physics.Raycast(_raySource.position, _raySource.forward, out hit, 1f, LayerUtil.DefaultEquivalent))
                {
                    if (hit.collider != null)
                    {
                        bool primary = false;
                        bool secondary = false;

                        if (hit.collider.gameObject.name == "PrimaryDoorCollider")
                            primary = true;
                        else if (hit.collider.gameObject.name == "SecondaryDoorCollider")
                            secondary = true;

                        if (primary || secondary)
                        {
                            Part p = FlightGlobals.GetPartUpwardsCached(hit.collider.gameObject);// Part.GetComponentUpwards<Part>(hit.collider.gameObject);

                            if (p != null)
                            {
                                IScalarModule scalar = null;
                                float deployLimit = 1;
                                PartModule USAnimate = null;

                                for (int i = p.Modules.Count - 1; i >= 0; i--)
                                {
                                    if (p.Modules[i].moduleName == "USAnimateGeneric")
                                    {
                                        USAnimate = p.Modules[i];

                                        if (USAnimate is IScalarModule)
                                            scalar = USAnimate as IScalarModule;

                                        break;
                                    }
                                }

                                if (USAnimate != null && scalar != null)
                                {
                                    BaseEvent doorEvent = null;
                                    BaseField doorLimit = null;

                                    if (primary)
                                    {
                                        doorEvent = USAnimate.Events["toggleEventPrimary"];

                                        doorLimit = USAnimate.Fields["primaryDeployLimit"];
                                    }
                                    else if (secondary)
                                    {
                                        doorEvent = USAnimate.Events["toggleEventSecondary"];

                                        doorLimit = USAnimate.Fields["secondaryDeployLimit"];
                                    }

                                    if (doorLimit != null)
                                        deployLimit = doorLimit.GetValue<float>(USAnimate) * 0.01f;

                                    if (doorEvent != null)
                                    {
                                        if (doorEvent.active && doorEvent.guiActive)
                                        {
                                            doorEvent.Invoke();

                                            DMUtils.Logging("Door Invoked");

                                            while (scalar.GetScalar < deployLimit)
                                                yield return null;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            var ownColliders = part.GetComponentsInChildren<Collider>();

                            bool flag = false;

                            for (int i = ownColliders.Length - 1; i >= 0; i--)
                            {
                                if (hit.collider == ownColliders[i])
                                {
                                    flag = true;

                                    break;
                                }
                            }

                            if (!flag)
                            {
                                ScreenMessages.PostScreenMessage(
                                    string.Format(
                                    "<b><color=orange>Obstruction detected preventing {0} from being deployed.</color></b>"
                                    , part.partInfo.title)
                                    , 5f, ScreenMessageStyle.UPPER_CENTER);

                                yield break;
                            }
                        }
                    }
                }
            }

            IsDeployed = true;

			animator(1f, 0f, Anim, animationName);

			if (USScience)
				animator(1f, 0f, USAnim, USBayAnimation);

			yield return new WaitForSeconds(Anim[animationName].length);

			fullyDeployed = true;
		}

		private IEnumerator retractEvent()
		{
			fullyDeployed = false;

			while (dishArm.localEulerAngles.z > 1 || dish.localEulerAngles.y > 1)
				yield return null;

			IsDeployed = false;

			animator(-1f, 1f, Anim, animationName);

			if (USScience)
			{
				if (Anim[animationName].length > USAnim[USBayAnimation].length && USAnim[USBayAnimation].length != 0)
					animator(-1f, (Anim[animationName].length / USAnim[USBayAnimation].length), USAnim, USBayAnimation);
				else
					animator(-1f, 1f, USAnim, USBayAnimation);
			}
		}

		#endregion

		#region Events and Actions

		[KSPEvent(guiActive = true, guiName = "Toggle Asteroid Scanner", active = true)]
		public void toggleEvent()
		{
			if (IsDeployed)
			{
				if (!fullyDeployed)
					StopCoroutine("deployEvent");

				StartCoroutine("retractEvent");
			}
			else
			{
				if (fullyDeployed)
					StopCoroutine("retractEvent");

				StartCoroutine("deployEvent");
			}
		}

		[KSPAction("Toggle Asteroid Scanner")]
		public void toggleAction(KSPActionParam param)
		{
			toggleEvent();
		}

		[KSPEvent(guiActive = true, guiName = "Toggle Asteroid Targeting line", active = false)]
		public void toggleLine()
		{
			showLines = !showLines;
		}

		[KSPEvent(guiActiveEditor = true, guiName = "Deploy Asteroid Scanner", active = true)]
		public void editorDeployEvent()
		{
			StartCoroutine(deployEvent());
			IsDeployed = false;
			Events["editorDeployEvent"].active = false;
			Events["editorRetractEvent"].active = true;
		}

		[KSPEvent(guiActiveEditor = true, guiName = "Retract Asteroid Scanner", active = false)]
		public void editorRetractEvent()
		{
			StartCoroutine(retractEvent());
			Events["editorDeployEvent"].active = true;
			Events["editorRetractEvent"].active = false;
		}

		[KSPEvent(guiActive = true, guiName = "Transfer Data", active = false)]
		public void TransferDataEvent()
		{
			if (PartItemTransfer.Instance != null)
			{
				ScreenMessages.PostScreenMessage("<b><color=orange>A transfer is already in progress.</color></b>", 3f, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			ExperimentTransfer.Create(part, this, new Callback<PartItemTransfer.DismissAction, Part>(transferData));
		}

		private void transferData(PartItemTransfer.DismissAction dismiss, Part p)
		{
			if (dismiss != PartItemTransfer.DismissAction.ItemMoved)
				return;

			if (p == null)
				return;

			if (scienceReports.Count <= 0)
			{
				ScreenMessages.PostScreenMessage(string.Format("[{0}]: has no data to transfer.", part.partInfo.title), 6, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			ModuleScienceContainer container = p.FindModuleImplementing<ModuleScienceContainer>();

			if (container == null)
			{
				ScreenMessages.PostScreenMessage(string.Format("<color=orange>[{0}]: {1} has no data container, canceling transfer.<color>", part.partInfo.title, p.partInfo.title), 6, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			if (!rerunnable)
			{
				List<DialogGUIBase> dialog = new List<DialogGUIBase>();
				dialog.Add(new DialogGUIButton<ModuleScienceContainer>("Remove Data", new Callback<ModuleScienceContainer>(onTransferData), container));
				dialog.Add(new DialogGUIButton("Cancel", null, true));

				PopupDialog.SpawnPopupDialog(
					new Vector2(0.5f, 0.5f),
					new Vector2(0.5f, 0.5f),
					new MultiOptionDialog(
						"TransferWarning",
						"Removing the experiment data will render this module inoperable.\n\nRestoring functionality will require a Scientist.",
						part.partInfo.title + "Warning!",
						UISkinManager.defaultSkin,
						dialog.ToArray()
						),
					false,
					UISkinManager.defaultSkin,
					true,
					""
					);
			}
			else
				onTransferData(container);
		}

		private void onTransferData(ModuleScienceContainer target)
		{
			if (target == null)
				return;

			int i = scienceReports.Count;

			if (target.StoreData(new List<IScienceDataContainer> { this }, false))
				ScreenMessages.PostScreenMessage(string.Format("[{0}]: {1} Data stored.", target.part.partInfo.title, i), 6, ScreenMessageStyle.UPPER_LEFT);
			else
				ScreenMessages.PostScreenMessage(string.Format("<color=orange>[{0}]: Not all data was stored.</color>", target.part.partInfo.title), 6, ScreenMessageStyle.UPPER_LEFT);
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, externalToEVAOnly = true, guiName = "Reset Asteroid Scanner", active = false)]
		public void ResetExperiment()
		{
			if (scienceReports.Count > 0)
			{
				scienceReports.Clear();
				Deployed = false;
			}
		}

		[KSPAction("Reset Asteroid Scanner")]
		public void ResetAction(KSPActionParam param)
		{
			ResetExperiment();
		}

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, guiName = "Collect Asteroid Data", active = false)]
		public void CollectDataExternalEvent()
		{
			List<ModuleScienceContainer> EVACont = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();
			if (scienceReports.Count > 0)
			{
				if (EVACont.First().StoreData(new List<IScienceDataContainer> { this }, false))
					DumpData(scienceReports[0]);
			}
		}

		[KSPEvent(guiActive = true, guiName = "Review Data", active = false)]
		public void ReviewDataEvent()
		{
			ReviewData();
		}

		[KSPAction("Review Data")]
		public void ReviewDataAction(KSPActionParam param)
		{
			ReviewData();
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, externalToEVAOnly = true, guiName = "Scan Asteroid Interior", active = false)]
		public void DeployExperiment()
		{
			gatherScienceData();
		}

		public void gatherScienceData(bool silent = false)
		{
			if (canConduct())
			{
				ModuleAsteroid modAst = null;
				float distance = asteroidScanLength(out modAst);
				runExperiment(distance, modAst, silent);
			}
			else
				ScreenMessages.PostScreenMessage(failMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
		}

		[KSPAction("Scan Asteroid")]
		public void DeployAction(KSPActionParam param)
		{
			DeployExperiment();
		}

		#endregion

		#region Science Setup

		public bool canConduct()
		{
			if (!receiverInRange)
			{
				failMessage = "No receivers detected within scanning range";
				return false;
			}
			else if (!asteroidInSight)
			{
				failMessage = "No valid targets within scanning range";
				return false;
			}

			if (FlightGlobals.ActiveVessel.isEVA)
			{
				if (!ScienceUtil.RequiredUsageExternalAvailable(part.vessel, FlightGlobals.ActiveVessel, (ExperimentUsageReqs)usageReqMaskExternal, exp, ref usageReqMessage))
				{
					failMessage = usageReqMessage;
					return false;
				}
			}

			return true;
		}

		//Determine if the signal passes through the asteroid, and what distance it travels if so
		private float asteroidScanLength(out ModuleAsteroid m)
		{
			m = null;
			float dist = 0f;
			if (dish != null && targetModule != null)
			{
				Vector3 tPos = dish.position;
				Vector3 targetPos = targetModule.part.transform.position;
				Vector3 direction = targetPos - tPos;
				Ray r = new Ray(tPos, direction);
				RaycastHit hit = new RaycastHit();
				Physics.Raycast(r, out hit, targetDistance, 1 << 0);
				DMUtils.DebugLog("Target Distance: {0:N3}", targetDistance);

				//The first ray determines whether or not the asteroid was hit and the distance from the dish
				//to that first encounter
				if (hit.collider != null)
				{
					float firstDist = hit.distance;
					DMUtils.DebugLog("First Ray Hit; Distance: {0:N3}", firstDist);
					Vector3 reverseDirection = tPos - targetPos;
					Ray targetRay = new Ray(targetPos, reverseDirection);
					RaycastHit targetHit = new RaycastHit();
					Physics.Raycast(targetRay, out targetHit, targetDistance, 1 << 0);

					//The second ray determines the distance from the target vessel to the asteroid
					if (targetHit.collider != null)
					{
						float secondDist = targetHit.distance;
						DMUtils.DebugLog("Second Ray Hit; Distance: {0:N3}", secondDist);
						Part p = Part.FromGO(hit.transform.gameObject) ?? hit.transform.gameObject.GetComponentInParent<Part>();

						if (p != null)
						{
							if (p.Modules.Contains("ModuleAsteroid"))
								m = p.FindModuleImplementing<ModuleAsteroid>();
						}

						//The two distances are subtracted from the total distance between vessels to 
						//give the distance the signal travels while inside the asteroid
						dist = targetDistance - secondDist - firstDist;

						DMUtils.DebugLog("Asteroid Scan Distance: {0:N3}", dist);
					}
				}
			}

			return dist;
		}

		private void runExperiment(float distance, ModuleAsteroid m, bool silent)
		{
			ScienceData data = makeScience(distance, m);
			if (data == null)
				Debug.LogError("[DM] Something Went Wrong Here; Null Asteroid Science Data Returned; Please Report This On The KSP Forum With Output.log Data");
			else
			{
				GameEvents.OnExperimentDeployed.Fire(data);
				scienceReports.Add(data);
				Deployed = true;
				if (!silent)
					ReviewData();
			}
		}

		private int aClassInt(string s)
		{
			switch (s)
			{
				case "Class A":
					return 0;
				case "Class B":
					return 1;
				case "Class C":
					return 2;
				case "Class D":
					return 3;
				case "Class E":
					return 4;
				default:
					return 5;
			}
		}

		private ScienceData makeScience(float dist, ModuleAsteroid m)
		{
			if (dist <= 0)
			{
				DMUtils.Logging("Asteroid Not Scanned...  Distance Passed Through Asteroid: " + dist.ToString("N3"));
				if (asteroidInSight)
					ScreenMessages.PostScreenMessage("No Asteroid Detected Between The Transmitting And Receiving Instruments...", 6f, ScreenMessageStyle.UPPER_CENTER);
				else if (receiverInRange)
					ScreenMessages.PostScreenMessage("No Asteroid Detected In The Scanning Area...", 6f, ScreenMessageStyle.UPPER_CENTER);
				return null;
			}
			if (m == null)
			{
				DMUtils.Logging("Asteroid Not Scanned. Something Went Wrong Here; No Asteroid Was Detected; Distance Passed Through Asteroid: " + dist.ToString("N3"));
				return null;
			}
			ScienceData data = null;
			ScienceSubject sub = null;
			CelestialBody body = null;
			DMAsteroidScience ast = null;
			string biome = "";
			float multiplier = 1f;

			ast = new DMAsteroidScience(m);
			body = ast.Body;
			biome = ast.AType + ast.ASeed;
			multiplier = Math.Min(1f, dist / astWidth[aClassInt(ast.AClass)]);

			if (exp == null)
			{
				Debug.LogError("[DM] Something Went Wrong Here; Null Asteroid Experiment Returned; Please Report This On The KSP Forum With Output.log Data");
				return null;
			}

			sub = ResearchAndDevelopment.GetExperimentSubject(exp, ExperimentSituations.InSpaceLow, body, biome, "");

			if (sub == null)
			{
				Debug.LogError("[DM] Something Went Wrong Here; Null Asteroid Subject Returned; Please Report This On The KSP Forum With Output.log Data");
				return null;
			}

			DMUtils.OnAsteroidScience.Fire(ast.AClass, exp.id);

			string b = ast.AType;

			string a = "a";
			if (b == "Icy-Organic")
				a = "an";

			string c = " asteroid";
			if (b == "Comet")
				c = "";

			sub.title = string.Format("{0} through {1} {2}{3}", exp.experimentTitle, a, b, c);
			string dataTitle = string.Format("{0} through {1:P0} of {2} {3}{4}", exp.experimentTitle, multiplier, a, b, c);
			registerDMScience(ast, sub);
			body.bodyName = asteroidBodyNameFixed;

			data = new ScienceData(multiplier * exp.baseValue * sub.dataScale, transmitValue, 0, sub.id, dataTitle, false, part.flightID);

			return data;
		}

		private void registerDMScience(DMAsteroidScience newAst, ScienceSubject sub)
		{
			if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX)
				return;

			DMScienceData DMData = null;
			DMScienceData DMScience = DMScienceScenario.SciScenario.getDMScience(sub.title);
			if (DMScience != null)
			{
				sub.scientificValue *= DMScience.SciVal;
				DMData = DMScience;
			}

			if (DMData == null)
			{
				float astSciCap = exp.scienceCap * 25f;
				DMScienceScenario.SciScenario.RecordNewScience(sub.title, exp.baseValue, 1f, 0f, astSciCap);
				sub.scientificValue = 1f;
			}
			sub.subjectValue = newAst.SciMult;
			sub.scienceCap = exp.scienceCap * sub.subjectValue;
			sub.science = Math.Max(0f, Math.Min(sub.scienceCap, sub.scienceCap - (sub.scienceCap * sub.scientificValue)));
		}

		#endregion

		#region Results Page

		private void experimentResultsPage(ScienceData data)
		{
			if (scienceReports.Count > 0)
			{
				ExperimentResultDialogPage page = new ExperimentResultDialogPage(part, data, data.baseTransmitValue, data.transmitBonus, false, "", true, new ScienceLabSearch(vessel, data), new Callback<ScienceData>(onDiscardData), new Callback<ScienceData>(onKeepData), new Callback<ScienceData>(onTransmitData), new Callback<ScienceData>(onSendToLab));
				resultsDialog = ExperimentsResultDialog.DisplayResult(page);
			}
		}

		private void onDiscardData(ScienceData data)
		{
			resultsDialog = null;
			if (scienceReports.Count > 0)
			{
				scienceReports.Clear();
				Deployed = false;
			}
		}

		private void onKeepData(ScienceData data)
		{
			resultsDialog = null;
		}

		private void onTransmitData(ScienceData data)
		{
			resultsDialog = null;

			IScienceDataTransmitter bestTransmitter = ScienceUtil.GetBestTransmitter(vessel);
			if (bestTransmitter != null)
			{
				bestTransmitter.TransmitData(new List<ScienceData> { data });
				DumpData(data);
			}
			else if (CommNet.CommNetScenario.CommNetEnabled)
				ScreenMessages.PostScreenMessage("No usable, in-range Comms Devices on this vessel. Cannot Transmit Data.", 3f, ScreenMessageStyle.UPPER_CENTER);
			else
				ScreenMessages.PostScreenMessage("No Comms Devices on this vessel. Cannot Transmit Data.", 3f, ScreenMessageStyle.UPPER_CENTER);
		}

		private void onSendToLab(ScienceData data)
		{
			resultsDialog = null;

			ScienceLabSearch labSearch = new ScienceLabSearch(vessel, data);

			if (labSearch.NextLabForDataFound)
			{
				StartCoroutine(labSearch.NextLabForData.ProcessData(data, null));
				DumpData(data);
			}
			else
				labSearch.PostErrorToScreen();
		}

		#endregion

		#region IScienceDataContainer

		public void ReviewData()
		{
			if (scienceReports.Count > 0)
				experimentResultsPage(scienceReports[0]);
		}

		public void ReviewDataItem(ScienceData data)
		{
			ReviewData();
		}

		public void ReturnData(ScienceData data)
		{
			if (data == null)
				return;

			scienceReports.Add(data);
			Inoperable = false;
			Deployed = true;
		}

		public bool IsRerunnable()
		{
			return rerunnable;
		}

		public int GetScienceCount()
		{
			return scienceReports.Count;
		}

		public ScienceData[] GetData()
		{
			return scienceReports.ToArray();
		}

		public void DumpData(ScienceData data)
		{
			if (scienceReports.Count > 0)
			{
				Inoperable = !IsRerunnable();
				Deployed = Inoperable;
				scienceReports.Remove(data);
			}
		}

		#endregion

	}
}
