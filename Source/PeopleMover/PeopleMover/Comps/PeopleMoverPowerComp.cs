using RimWorld;
using Verse;

namespace DuneRef_PeopleMover
{
    public class PeopleMoverPowerComp : CompPowerTrader
    {
        static public float modSettingsHubPowerOutput = (float)PeopleMoverSettings.wattageHub;
        public float desiredPowerOutput = modSettingsHubPowerOutput;

        public override void CompTick()
        {
            base.CompTick();

            this.UpdateDesiredPowerOutput(desiredPowerOutput);
        }
        public virtual void UpdateDesiredPowerOutput(float desiredPowerOutput)
        {
            this.desiredPowerOutput = desiredPowerOutput;

            if ((this.flickableComp != null && !this.flickableComp.SwitchIsOn) || !base.PowerOn)
            {
                base.PowerOutput = 0f;
                return;
            }

            base.PowerOutput = desiredPowerOutput;
        }
    }
}