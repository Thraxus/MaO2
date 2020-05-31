using MaO2.Common.BaseClasses;
using MaO2.Common.Enums;
using MaO2.Models;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;

namespace MaO2
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, priority: int.MinValue + 1)]
	public class MaCore : BaseSessionComp
	{
		protected override string CompName { get; } = "MaCore";
		protected override CompType Type { get; } = CompType.Server;
		protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.NoUpdate;

		private readonly MyConcurrentList<OxyGen> _generators = new MyConcurrentList<OxyGen>();

		protected override void SuperEarlySetup()
		{
			base.SuperEarlySetup();
			MyEntities.OnEntityCreate += OnEntityCreate;
		}

		private void OnEntityCreate(MyEntity ent)
		{
			IMyGasGenerator generator = ent as IMyGasGenerator;
			if (generator == null) return;
			if (generator.BlockDefinition.SubtypeId != "MA_O2") return;
			OxyGen oxy = new OxyGen(generator);
			oxy.PruneMe += CleanList;
			oxy.OnWriteToLog += WriteToLog;
			oxy.Report();
			_generators.Add(oxy);
		}

		private void CleanList(OxyGen closedEntity)
		{
			_generators.Remove(closedEntity);
		}

		protected override void Unload()
		{
			MyEntities.OnEntityCreate -= OnEntityCreate;
			foreach (OxyGen generator in _generators)
			{
				generator.Close();
				generator.OnWriteToLog -= WriteToLog;
				generator.PruneMe -= CleanList;
			}
			base.Unload();
		}
	}
}