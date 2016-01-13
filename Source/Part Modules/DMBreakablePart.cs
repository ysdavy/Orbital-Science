﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DMagic.Part_Modules
{
	public class DMBreakablePart : DMModuleScienceAnimate
	{
		private List<GameObject> breakableObjects = new List<GameObject>();
		private Transform baseTransform;

		[KSPField(isPersistant = true)]
		public bool broken;
		[KSPField]
		public string baseTransfromName = "";
		[KSPField]
		public float componentDrag = 0.5f;
		[KSPField]
		public float componentMass = 0.001f;

		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);

			if (!string.IsNullOrEmpty(baseTransfromName))
				baseTransform = part.FindModelTransform(baseTransfromName);

			if (broken)
			{
				setTransformState(false);
			}
		}

		public override void OnFixedUpdate()
		{
			base.OnFixedUpdate();
		}

		public override void deployEvent()
		{
			base.deployEvent();

			if (broken && oneShot && !oneWayAnimation)
				base.Events["retractEvent"].active = true;
		}

		private void setTransformState(bool on)
		{
			if (baseTransform == null)
				return;

			baseTransform.gameObject.SetActive(on);
		}

		private void breakObjects()
		{
			if (broken)
				return;

			getChildren(baseTransform);

			for (int i = 0; i < breakableObjects.Count; i++)
			{
				GameObject o = breakableObjects[i];

				if (o == null)
					continue;

				Rigidbody r = o.AddComponent<Rigidbody>();

				if (r == null)
					continue;

				Vector3 randomAngular = new Vector3();
				r.angularVelocity = part.rigidbody.angularVelocity + randomAngular;
				Vector3 randomVel = new Vector3();
				Vector3 localCOM = vessel.findWorldCenterOfMass() - part.rigidbody.worldCenterOfMass;
				r.velocity = part.rigidbody.velocity + randomVel + Vector3.Cross(localCOM, rigidbody.angularVelocity);
				r.mass = componentMass;
				r.useGravity = false;
				o.transform.parent = null;
				physicalObject p = o.AddComponent<physicalObject>();
				r.drag = componentDrag;
			}

			if (IsDeployed && oneShot && !oneWayAnimation)
				base.Events["retractEvent"].active = true;
		}

		private void getChildren(Transform t)
		{
			if (t == null)
				return;

			for (int i = 0; i < t.childCount; i++)
			{
				Transform tChild = t.GetChild(i);

				if (tChild == null)
					continue;

				GameObject obj = tChild.gameObject;

				if (obj == null)
					continue;

				if (obj.GetComponent<MeshRenderer>() == null)
					continue;

				breakableObjects.Add(obj);

				getChildren(tChild);
			}
		}

		protected void onBreak()
		{
			breakObjects();

			broken = true;
		}

		protected void onFix()
		{
			broken = false;

			setTransformState(true);
		}
	}
}
