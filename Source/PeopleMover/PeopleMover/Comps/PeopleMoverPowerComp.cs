using RimWorld;
using UnityEngine;
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

            float powerDraw = (float)(-1.0 * desiredPowerOutput);
            base.PowerOutput = powerDraw;
        }

        // Replacement for inspect string since it doesn't handle negative values properly.
        public override string CompInspectStringExtra()
        {
            string text;
            if (this.powerLastOutputted && !base.Props.alwaysDisplayAsUsingPower)
            {
                text = "PowerOutput".Translate() + ": " + this.PowerOutput.ToString("#####0") + " W";
            }
            else if (base.Props.idlePowerDraw >= 0f && Mathf.Approximately(this.PowerOutput, -base.Props.idlePowerDraw))
            {
                text = "PowerNeeded".Translate() + ": " + base.Props.idlePowerDraw.ToString("#####0") + " W";
                text += " (" + "PowerActiveNeeded".Translate(this.desiredPowerOutput.ToString("#####0")) + ")";
            }
            else
            {
                text = "PowerNeeded".Translate() + ": " + this.desiredPowerOutput.ToString("#####0") + " W";
            }

            if (this.PowerNet == null)
            {
                text += "\n" + "PowerNotConnected".Translate();
            }
            else
            {
                string value = (this.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick).ToString("F0");
                string value2 = this.PowerNet.CurrentStoredEnergy().ToString("F0");
                text += "\n" + "PowerConnectedRateStored".Translate(value, value2);
            }

            return text;
        }
    }
}