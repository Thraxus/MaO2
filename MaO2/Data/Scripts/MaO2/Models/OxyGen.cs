using System;
using System.Text;
using MaO2.Common.BaseClasses;
using MaO2.Common.Enums;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;

namespace MaO2.Models
{
	public class OxyGen : BaseClosableLoggingClass
	{
		protected sealed override string Id { get; } = "OxyGen";

		public override void Close()
		{
			base.Close();
			_thisGenerator.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
			_thisGenerator.AppendingCustomInfo -= AppendingCustomInfo;
			MySink.RequiredInputChanged -= OnRequiredInputChanged;
			_thisGenerator.OnClose -= OnClose;
			WriteToLog("Close:", $"I'm out! {_thisGenerator.EntityId}", LogType.General);
		}

		public event Action<OxyGen> PruneMe;

		private readonly IMyGasGenerator _thisGenerator;
		private readonly IMyTerminalBlock _thisTerminalBlock;
		private readonly MyCubeBlock _thisCubeBlock;
		private readonly MyEntity _thisEntity;
		
		private const string Power = "PowerEfficiency";
		private const string Yield = "Effectiveness";
		private const string Speed = "Productivity";
		private const float BasePowerConsumptionMultiplier = 1f;
		private const float BaseProductionCapacityMultiplier = 1f;

		private const float KshDefaultMultiplier = 150f;
		private readonly float _baseOxyMaxOutput;
		private readonly float _baseHydroMaxOutput;

		private readonly MyDefinitionId _oxyDef = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Oxygen");
		private readonly MyDefinitionId _hydroDef = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Hydrogen");

		private MyResourceSinkComponent MySink => ((MyResourceSinkComponent)_thisTerminalBlock.ResourceSink);

		private MyResourceSourceComponent MyResource => _thisCubeBlock.Components.Get<MyResourceSourceComponent>();

		public OxyGen(IMyGasGenerator thisGenerator)
		{
			Id = $"{Id} ({thisGenerator.EntityId})";
			_thisGenerator = thisGenerator;
			_thisTerminalBlock = thisGenerator;
			_thisCubeBlock = (MyCubeBlock) thisGenerator;
			_thisEntity = (MyEntity) thisGenerator;
			_thisGenerator.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
			_thisGenerator.AppendingCustomInfo += AppendingCustomInfo;
			_thisEntity.AddedToScene += OnAddedToScene;
			MySink.RequiredInputChanged += OnRequiredInputChanged;
			_thisGenerator.OnClose += OnClose;
			_thisGenerator.AddUpgradeValue(Power, 1f);
			_thisGenerator.AddUpgradeValue(Yield, 1f);
			_thisGenerator.AddUpgradeValue(Speed, 1f);

			MyObjectBuilder_OxygenGenerator x = (MyObjectBuilder_OxygenGenerator) _thisGenerator.GetObjectBuilderCubeBlock();

			MyOxygenGeneratorDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(x.GetId()) as MyOxygenGeneratorDefinition;
			if (def == null) return;
			foreach (MyOxygenGeneratorDefinition.MyGasGeneratorResourceInfo resource in def.ProducedGases)
			{
				if (resource.Id == _oxyDef)
					_baseOxyMaxOutput = KshDefaultMultiplier * resource.IceToGasRatio;
				if (resource.Id == _hydroDef)
					_baseHydroMaxOutput = KshDefaultMultiplier * resource.IceToGasRatio;
			}
		}

		private void OnRequiredInputChanged(MyDefinitionId unused1, MyResourceSinkComponent unused2, float unused3, float unused4)
		{
			_thisTerminalBlock.RefreshCustomInfo();
		}

		private void OnAddedToScene(MyEntity entity)
		{
			if (entity != _thisEntity) return;
			_thisTerminalBlock.RefreshCustomInfo();
			_thisEntity.AddedToScene -= OnAddedToScene;
		}

		private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder value)
		{
			if (block != _thisTerminalBlock) return;
			UpdateInfo(value);
			UpdateTerminal();
		}

		private void UpdateTerminal()
		{
			MyOwnershipShareModeEnum shareMode;
			long ownerId;
			if (_thisCubeBlock.IDModule != null)
			{
				ownerId = _thisCubeBlock.IDModule.Owner;
				shareMode = _thisCubeBlock.IDModule.ShareMode;
			}
			else
			{
				IMyTerminalBlock sorter = _thisCubeBlock as IMyTerminalBlock;
				if (sorter == null) return;
				sorter.ShowOnHUD = !sorter.ShowOnHUD;
				sorter.ShowOnHUD = !sorter.ShowOnHUD;
				return;
			}
			_thisCubeBlock.ChangeOwner(ownerId, shareMode == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
			_thisCubeBlock.ChangeOwner(ownerId, shareMode);
		}

		private void OnClose(IMyEntity closedEntity)
		{
			PruneMe?.Invoke(this);
			Close();
		}

		public void Report()
		{
			WriteToLog("Report:", $"I'm alive: {_thisGenerator.EntityId}", LogType.General);
		}

		private void UpdateInfo(StringBuilder detailedInfo)
		{
			//detailedInfo.Clear();
			detailedInfo.Append("\n");
			detailedInfo.Append("Actual Max Required: ");
			MyValueFormatter.AppendWorkInBestUnit(MySink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId), detailedInfo);
			detailedInfo.Append("\n");
			detailedInfo.Append("Current Power Use: ");
			MyValueFormatter.AppendWorkInBestUnit(_thisGenerator.ResourceSink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId), detailedInfo);
			detailedInfo.AppendFormat("\n\n");
			detailedInfo.Append("Power Efficiency: ");
			detailedInfo.Append(((1f/_thisGenerator.PowerConsumptionMultiplier) * 100f).ToString(" 0"));
			detailedInfo.Append("%\n");
			detailedInfo.Append("Resource Efficiency: ");
			detailedInfo.Append(((1f/_thisGenerator.ProductionCapacityMultiplier) * 100.0).ToString(" 0"));
			detailedInfo.Append("%\n");
			detailedInfo.Append("Speed Multiplier: ");
			detailedInfo.Append(((_thisGenerator.UpgradeValues[Speed]) * 100.0).ToString(" 0"));
			detailedInfo.Append("%\n");
		}

		private readonly object _syncLock = new object();
		
		private void OnUpgradeValuesChanged()
		{
			lock (_syncLock)
			{
				float power;
				float speed;
				float yield;

				if (!_thisGenerator.UpgradeValues.TryGetValue(Power, out power))
					power = 1;
				if (!_thisGenerator.UpgradeValues.TryGetValue(Yield, out yield))
					yield = 1;
				if (!_thisGenerator.UpgradeValues.TryGetValue(Speed, out speed))
					speed = 1;

				_thisGenerator.PowerConsumptionMultiplier = (BasePowerConsumptionMultiplier / power) * speed * yield; // Power Efficiency
				_thisGenerator.ProductionCapacityMultiplier = (BaseProductionCapacityMultiplier / (yield >= 1 ? yield : 1) * (speed > 1 ? (speed * 0.15f) + 1 : speed)); // Yield

				MyResource.SetMaxOutputByType(_oxyDef, _baseOxyMaxOutput * speed);
				MyResource.SetMaxOutputByType(_hydroDef, _baseHydroMaxOutput * speed);
			}
		}
	}
}