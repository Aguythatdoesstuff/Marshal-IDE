using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class OobParser : BaseParser
    {
        public OobMetadata Metadata { get; set; }

        public OobParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "OobParser initialized.");
        }

        public class Oob
        {
            public List<DivisionTemplate> DivisionTemplates { get; set; } = new List<DivisionTemplate>();
            public List<DivisionPlacement> DivisionPlacements { get; set; } = new List<DivisionPlacement>();
            public List<AirWingPlacement> AirWingPlacements { get; set; } = new List<AirWingPlacement>();
            public List<FleetPlacement> FleetPlacements { get; set; } = new List<FleetPlacement>();
            public List<AddProduction> AddProductions { get; set; } = new List<AddProduction>();
        }

        public class DivisionTemplate
        {
            public string Id { get; set; } = string.Empty;
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
            public SupportUnits SupportUnits { get; set; } = new SupportUnits();
        }
        public class SupportUnits
        {
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class DivisionPlacement
        {
            public string Id { get; set; } = string.Empty;
            public string Coordinates { get; set; } = string.Empty;
            public string ForeignId { get; set; } = string.Empty; // References DivisionTemplate Id
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class AirWingPlacement
        {
            public string Coordinates { get; set; } = string.Empty;
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class FleetPlacement
        {
            public string Id { get; set; } = string.Empty;
            public string Coordinates { get; set; } = string.Empty;
            public List<TaskForcePlacement> TaskForces { get; set; } = new List<TaskForcePlacement>();
        }

        public class TaskForcePlacement
        {
            public string Id { get; set; } = string.Empty;
            public string Coordinates { get; set; } = string.Empty;
            public List<Ship> Ships { get; set; } = new List<Ship>();
        }

        public class Ship
        {
            public string Id { get; set; } = string.Empty;
            public string ForeignId { get; set; } = string.Empty; // Archetype ID
            public string CountryTag { get; set; } = string.Empty;
            public string CategoryId { get; set; } = string.Empty;
            public string DesignId { get; set; } = string.Empty;
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class AddProduction
        {
            public string EquipmentId { get; set; } = string.Empty;
            public string CountryTag { get; set; } = string.Empty;
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class ParsedOobFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Oob> Oobs { get; set; } = new List<Oob>();
        }

        // Store the most recently parsed file for other components to use
        public ParsedOobFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            var parsedFile = new ParsedOobFile { SourceFileName = fileName };
            var currentOob = new Oob();
            parsedFile.Oobs.Add(currentOob);

            if (Metadata == null)
            {
                Errors.Add(new ParsingError(
                    fileName,
                    0,
                    $"ERROR! FAILED TO RETRIEVE ALL FILE METADATA FOR EQUIPMENT IN FILE: {filePath}."
                ));
                return;
            }

            DivisionTemplate currentTemplate = null;
            DivisionPlacement currentDivision = null;
            AirWingPlacement currentAirWing = null;
            FleetPlacement currentFleet = null;
            TaskForcePlacement currentTaskForce = null;
            Ship currentShip = null;
            AddProduction currentProduction = null;
            bool isInsideSupportUnits = false;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                string lineText = pl.TrimmedLine;

                // ==========================================
                // DEPTH 0: BLOCK HEADERS
                // ==========================================
                if (pl.Depth == 0)
                {
                    // Reset all active contexts
                    currentTemplate = null;
                    currentDivision = null;
                    currentAirWing = null;
                    currentFleet = null;
                    currentTaskForce = null;
                    currentShip = null;
                    currentProduction = null;
                    isInsideSupportUnits = false;

                    if (lineText.StartsWith("division template ", StringComparison.OrdinalIgnoreCase))
                    {
                        currentTemplate = new DivisionTemplate
                        {
                            Id = GetQuotedContent(lineText.Substring("division template ".Length))
                        };
                        currentOob.DivisionTemplates.Add(currentTemplate);
                    }
                    else if (lineText.StartsWith("place division ", StringComparison.OrdinalIgnoreCase))
                    {
                        string id = GetQuotedContent(lineText);
                        string coordinates = string.Empty;
                        string templateId = string.Empty;

                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            coordinates = lineData.Coordinates ?? string.Empty;
                        }

                        int usingIndex = lineText.IndexOf(" using template ", StringComparison.OrdinalIgnoreCase);
                        if (usingIndex != -1)
                        {
                            templateId = GetQuotedContent(lineText.Substring(usingIndex + " using template ".Length));
                        }

                        currentDivision = new DivisionPlacement
                        {
                            Id = id,
                            Coordinates = coordinates,
                            ForeignId = templateId
                        };
                        currentOob.DivisionPlacements.Add(currentDivision);
                    }
                    else if (lineText.StartsWith("place airwings at ", StringComparison.OrdinalIgnoreCase))
                    {
                        string coordinates = string.Empty;
                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            coordinates = lineData.Coordinates ?? string.Empty;
                        }

                        currentAirWing = new AirWingPlacement
                        {
                            Coordinates = coordinates
                        };
                        currentOob.AirWingPlacements.Add(currentAirWing);
                    }
                    else if (lineText.StartsWith("place fleet ", StringComparison.OrdinalIgnoreCase))
                    {
                        string id = GetQuotedContent(lineText);
                        string coordinates = string.Empty;
                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            coordinates = lineData.Coordinates ?? string.Empty;
                        }

                        currentFleet = new FleetPlacement
                        {
                            Id = id,
                            Coordinates = coordinates
                        };
                        currentOob.FleetPlacements.Add(currentFleet);
                    }
                    else if (lineText.StartsWith("add production ", StringComparison.OrdinalIgnoreCase))
                    {
                        string equipmentId = string.Empty;
                        string countryTag = string.Empty;

                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            equipmentId = lineData.Id ?? string.Empty;
                            countryTag = lineData.CountryTag ?? string.Empty;
                        }

                        currentProduction = new AddProduction
                        {
                            EquipmentId = equipmentId,
                            CountryTag = countryTag
                        };
                        currentOob.AddProductions.Add(currentProduction);
                    }

                    continue;
                }

                // ==========================================
                // NESTED CONTENT HANDLING (DEPTH >= 1)
                // ==========================================
                var raw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };

                if (currentTemplate != null)
                {
                    // Check if entering or exiting support units scope
                    if (lineText.StartsWith("support", StringComparison.OrdinalIgnoreCase))
                    {
                        isInsideSupportUnits = true;
                        continue; // Skip saving the 'support' block header line itself
                    }

                    if (isInsideSupportUnits)
                    {
                        // If we drop back to depth 1 (or root of template), exit support units context
                        if (pl.Depth == 1 && !lineText.StartsWith("support", StringComparison.OrdinalIgnoreCase))
                        {
                            isInsideSupportUnits = false;
                            currentTemplate.RawLines.Add(raw);
                        }
                        else
                        {
                            currentTemplate.SupportUnits.RawLines.Add(raw);
                        }
                    }
                    else
                    {
                        currentTemplate.RawLines.Add(raw);
                    }
                }
                else if (currentDivision != null)
                {
                    currentDivision.RawLines.Add(raw);
                }
                else if (currentAirWing != null)
                {
                    currentAirWing.RawLines.Add(raw);
                }
                else if (currentProduction != null)
                {
                    currentProduction.RawLines.Add(raw);
                }
                else if (currentFleet != null)
                {
                    if (pl.Depth == 1 && lineText.StartsWith("place taskforce ", StringComparison.OrdinalIgnoreCase))
                    {
                        currentShip = null;
                        string tfId = GetQuotedContent(lineText);
                        string coordinates = string.Empty;

                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            coordinates = lineData.Coordinates ?? string.Empty;
                        }

                        currentTaskForce = new TaskForcePlacement
                        {
                            Id = tfId,
                            Coordinates = coordinates
                        };
                        currentFleet.TaskForces.Add(currentTaskForce);
                    }
                    else if (currentTaskForce != null)
                    {
                        if (pl.Depth == 2 && lineText.StartsWith("ship ", StringComparison.OrdinalIgnoreCase))
                        {
                            string shipId = GetQuotedContent(lineText);
                            string archetypeId = string.Empty;
                            string countryTag = string.Empty;

                            if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                            {
                                archetypeId = lineData.Id ?? string.Empty;
                                countryTag = lineData.CountryTag ?? string.Empty;
                            }

                            currentShip = new Ship
                            {
                                Id = shipId,
                                ForeignId = archetypeId,
                                CountryTag = countryTag
                            };
                            currentTaskForce.Ships.Add(currentShip);
                        }
                        else if (currentShip != null && pl.Depth >= 3)
                        {
                            if (lineText.StartsWith("using design ", StringComparison.OrdinalIgnoreCase))
                            {
                                currentShip.DesignId = GetQuotedContent(lineText.Substring("using design ".Length));
                            }
                            else if (lineText.StartsWith("category ", StringComparison.OrdinalIgnoreCase))
                            {
                                currentShip.CategoryId = GetQuotedContent(lineText.Substring("category ".Length));
                            }
                            else
                            {
                                currentShip.RawLines.Add(raw);
                            }
                        }
                    }
                }
            }

            LastParsedFile = parsedFile;
        }
    }
}