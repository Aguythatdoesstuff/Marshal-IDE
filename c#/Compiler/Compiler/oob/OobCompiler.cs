using System;
using System.IO;
using System.Linq;
using static Compiler.OobParser;

namespace Compiler
{
    public class OobCompiler : BaseCompiler
    {
        // Parsed OOB data provided by the parser
        public OobParser.ParsedOobFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null) return;

            var sbOob = new System.Text.StringBuilder();

            if (PassedData.Oobs != null && PassedData.Oobs.Count > 0)
            {
                foreach (var oob in PassedData.Oobs)
                {
                    CompileOob(oob, sbOob);
                }
            }

            // Flush OOB buffer using WriteFile
            if (sbOob.Length > 0)
            {
                WriteFile("history/units/", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbOob.ToString());
                });
            }
        }

        private void CompileOob(OobParser.Oob oob, System.Text.StringBuilder sbOob)
        {
            // 1. Division Templates
            if (oob.DivisionTemplates != null && oob.DivisionTemplates.Count > 0)
            {
                foreach (var template in oob.DivisionTemplates)
                {
                    sbOob.AppendLine("division_template = {");
                    sbOob.AppendLine($"{Ident(1)}name = \"{template.Id}\"");
                    sbOob.AppendLine($"{Ident(1)}regiments = {{");
                    if (template.RawLines != null && template.RawLines.Count > 0)
                    {
                        sbOob.Append(RenderAllowedToString(template.RawLines, r => r.depth + 1, r => r.trimmedLine));
                    }
                    sbOob.AppendLine($"{Ident(1)}}}");
                    sbOob.AppendLine($"{Ident(1)}support  = {{");
                    if (template.SupportUnits != null && template.SupportUnits.RawLines != null && template.SupportUnits.RawLines.Count > 0)
                    {
                        sbOob.Append(RenderAllowedToString(template.SupportUnits.RawLines, r => r.depth + 1, r => r.trimmedLine));
                    }
                    sbOob.AppendLine($"{Ident(1)}}}");
                    sbOob.AppendLine("}\n");
                }
            }

            // 2. Units Block (Divisions & Fleets)
            bool hasDivisions = oob.DivisionPlacements != null && oob.DivisionPlacements.Count > 0;
            bool hasFleets = oob.FleetPlacements != null && oob.FleetPlacements.Count > 0;

            if (hasDivisions || hasFleets)
            {
                sbOob.AppendLine("units = {");

                // Land Divisions
                if (hasDivisions)
                {
                    foreach (var division in oob.DivisionPlacements)
                    {
                        sbOob.AppendLine($"{Ident(1)}division = {{");

                        if (!string.IsNullOrEmpty(division.Id))
                        {
                            sbOob.AppendLine($"{Ident(2)}name = \"{division.Id}\"");
                        }

                        if (!string.IsNullOrEmpty(division.Coordinates))
                        {
                            sbOob.AppendLine($"{Ident(2)}location = {division.Coordinates}");
                        }

                        if (!string.IsNullOrEmpty(division.ForeignId))
                        {
                            sbOob.AppendLine($"{Ident(2)}division_template = \"{division.ForeignId}\"");
                        }

                        if (division.RawLines != null && division.RawLines.Count > 0)
                        {
                            sbOob.Append(RenderAllowedToString(division.RawLines, r => r.depth + 1, r => r.trimmedLine));
                        }

                        sbOob.AppendLine($"{Ident(1)}}}");
                    }
                }

                // Fleets & Task Forces
                if (hasFleets)
                {
                    foreach (var fleet in oob.FleetPlacements)
                    {
                        sbOob.AppendLine($"{Ident(1)}fleet = {{");

                        if (!string.IsNullOrEmpty(fleet.Id))
                        {
                            sbOob.AppendLine($"{Ident(2)}name = \"{fleet.Id}\"");
                        }

                        if (!string.IsNullOrEmpty(fleet.Coordinates))
                        {
                            sbOob.AppendLine($"{Ident(2)}naval_base = {fleet.Coordinates}");
                        }

                        if (fleet.TaskForces != null && fleet.TaskForces.Count > 0)
                        {
                            foreach (var tf in fleet.TaskForces)
                            {
                                sbOob.AppendLine($"{Ident(2)}task_force = {{");

                                if (!string.IsNullOrEmpty(tf.Id))
                                {
                                    sbOob.AppendLine($"{Ident(3)}name = \"{tf.Id}\"");
                                }

                                if (!string.IsNullOrEmpty(tf.Coordinates))
                                {
                                    sbOob.AppendLine($"{Ident(3)}location = {tf.Coordinates}");
                                }

                                if (tf.Ships != null && tf.Ships.Count > 0)
                                {
                                    foreach (var ship in tf.Ships)
                                    {
                                        sbOob.AppendLine($"{Ident(3)}ship = {{");

                                        if (!string.IsNullOrEmpty(ship.Id))
                                        {
                                            sbOob.AppendLine($"{Ident(4)}name = \"{ship.Id}\"");
                                        }

                                        if (!string.IsNullOrEmpty(ship.CategoryId))
                                        {
                                            sbOob.AppendLine($"{Ident(4)}definition = {ship.CategoryId}");
                                        }

                                        if (!string.IsNullOrEmpty(ship.ForeignId))
                                        {
                                            sbOob.AppendLine($"{Ident(4)}equipment = {{");
                                            sbOob.AppendLine($"{Ident(5)}{ship.ForeignId} = {{");
                                            sbOob.AppendLine($"{Ident(6)}amount = 1");

                                            if (!string.IsNullOrEmpty(ship.CountryTag))
                                            {
                                                sbOob.AppendLine($"{Ident(6)}owner = {ship.CountryTag}");
                                            }

                                            if (!string.IsNullOrEmpty(ship.DesignId))
                                            {
                                                sbOob.AppendLine($"{Ident(6)}version_name = \"{ship.DesignId}\"");
                                            }

                                            sbOob.AppendLine($"{Ident(5)}}}");
                                            sbOob.AppendLine($"{Ident(4)}}}");
                                        }

                                        if (ship.RawLines != null && ship.RawLines.Count > 0)
                                        {
                                            sbOob.Append(RenderAllowedToString(ship.RawLines, r => r.depth + 2, r => r.trimmedLine));
                                        }

                                        sbOob.AppendLine($"{Ident(3)}}}");
                                    }
                                }

                                sbOob.AppendLine($"{Ident(2)}}}");
                            }
                        }

                        sbOob.AppendLine($"{Ident(1)}}}");
                    }
                }

                sbOob.AppendLine("}\n");
            }

            // 3. Air Wings
            if (oob.AirWingPlacements != null && oob.AirWingPlacements.Count > 0)
            {
                sbOob.AppendLine("air_wings = {");

                foreach (var airWing in oob.AirWingPlacements)
                {
                    if (!string.IsNullOrEmpty(airWing.Coordinates))
                    {
                        sbOob.AppendLine($"{Ident(1)}{airWing.Coordinates} = {{");
                    }
                    else
                    {
                        sbOob.AppendLine($"{Ident(1)}0 = {{");
                    }

                    if (airWing.RawLines != null && airWing.RawLines.Count > 0)
                    {
                        sbOob.Append(RenderAllowedToString(airWing.RawLines, r => r.depth + 1, r => r.trimmedLine));
                    }

                    sbOob.AppendLine($"{Ident(1)}}}");
                }

                sbOob.AppendLine("}\n");
            }

            // 4. Equipment Production (instant_effect)
            if (oob.AddProductions != null && oob.AddProductions.Count > 0)
            {
                sbOob.AppendLine("instant_effect = {");

                foreach (var prod in oob.AddProductions)
                {
                    sbOob.AppendLine($"{Ident(1)}add_equipment_production = {{");

                    if (!string.IsNullOrEmpty(prod.EquipmentId) || !string.IsNullOrEmpty(prod.CountryTag))
                    {
                        sbOob.AppendLine($"{Ident(2)}equipment = {{");

                        if (!string.IsNullOrEmpty(prod.EquipmentId))
                        {
                            sbOob.AppendLine($"{Ident(3)}type = {prod.EquipmentId}");
                        }

                        if (!string.IsNullOrEmpty(prod.CountryTag))
                        {
                            sbOob.AppendLine($"{Ident(3)}creator = \"{prod.CountryTag}\"");
                        }

                        sbOob.AppendLine($"{Ident(2)}}}");
                    }

                    if (prod.RawLines != null && prod.RawLines.Count > 0)
                    {
                        sbOob.Append(RenderAllowedToString(prod.RawLines, r => r.depth + 1, r => r.trimmedLine));
                    }

                    sbOob.AppendLine($"{Ident(1)}}}");
                }

                sbOob.AppendLine("}\n");
            }
        }
    }
}