﻿using Common.Constants;
using Common.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Common.Commands
{
    public class Commands
    {
        #region Coordinates 
        [CommandHelp(".gps")]
        public static void Gps(IWorldManager manager, string[] args)
        {
            var character = manager.Account.ActiveCharacter;
            manager.Send(character.BuildMessage(character.Location.ToString()));
        }
        #endregion

        #region Teleport

        [CommandHelp(".go {x} {y} {z} Optional: {mapid}")]
        [CommandHelp(".go {name}")]
        [CommandHelp(".go instance {name | id}")]

        public static void Go(IWorldManager manager, string[] args)
        {
            if (args.Length == 0)
                return;

            if (Read(args, 0, out float test)) //Co-ordinate port
                GoLocation(manager, args);
            else if (args[0].ToLower().Trim() == "instance") //Area Trigger
                GoTrigger(manager, args);
            else
                GoNamedArea(manager, true, args); //Worldport
        }


        private static void GoNamedArea(IWorldManager manager, bool worldport, string[] args)
        {
            int skip = args[0] == "area" || args[0] == "instance" ? 1 : 0;
            string needle = string.Join(" ", args.Skip(skip).ToArray()); //Replace "area" and "instance"

            var locations = worldport ? Worldports.FindLocation(needle) : AreaTriggers.FindTrigger(needle);
            switch (locations.Count())
            {
                case 0: //No matches
                    manager.Send(manager.Account.ActiveCharacter.BuildMessage("No matching locations found"));
                    break;
                case 1: //Single match
                    var loc = locations.First().Value;
                    manager.Account.ActiveCharacter.Teleport(loc.X, loc.Y, loc.Z, loc.O, loc.Map, ref manager);
                    break;
                default: //Multiple possible matches
                    manager.Send(manager.Account.ActiveCharacter.BuildMessage("Multiple matches:"));

                    foreach (var l in locations)
                        manager.Send(manager.Account.ActiveCharacter.BuildMessage(" " + l.Key));

                    break;
            }
        }

        private static void GoLocation(IWorldManager manager, string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
                return;

            var character = manager.Account.ActiveCharacter;
            uint map = character.Location.Map;
            bool teleport = true;

            if (teleport &= Read(args, 0, out float x))
                character.BuildMessage($"Invalid X parameter.");

            if (teleport &= Read(args, 1, out float y))
                character.BuildMessage($"Invalid Y parameter.");

            if (teleport &= Read(args, 2, out float z))
                character.BuildMessage($"Invalid Z parameter.");

            if (args.Length > 3 && (teleport &= Read(args, 3, out map)))
                character.BuildMessage($"Invalid Map parameter.");

            if (teleport)
                character.Teleport(x, y, z, character.Location.O, map, ref manager);
        }

        private static void GoTrigger(IWorldManager manager, string[] args)
        {
            if (args.Length < 2)
                return;

            var character = manager.Account.ActiveCharacter;
            if (uint.TryParse(args[1], out uint areaid)) //Area Id check
            {
                if (AreaTriggers.Triggers.ContainsKey(areaid))
                {
                    var area = AreaTriggers.Triggers[areaid];
                    character.Teleport(area.X, area.Y, area.Z, area.O, area.Map, ref manager);
                }
                else
                {
                    manager.Send(character.BuildMessage($"Area Id {areaid} does not exist"));
                }
            }
            else
            {
                GoNamedArea(manager, false, args); //Area name check
            }
        }
        #endregion

        #region Nudge
        [CommandHelp(".nudge Optional: [1 - 100]")]
        public static void Nudge(IWorldManager manager, string[] args)
        {
            var character = manager.Account.ActiveCharacter;
            var loc = character.Location;

            Read(args, 0, out float force);
            force = Math.Min(Math.Max((int)force, 1), 100); //Min 1 Max 100

            float X = (float)(loc.X + Math.Cos(loc.O) * force);
            float Y = (float)(loc.Y + Math.Sin(loc.O) * force);
            character.Teleport(X, Y, loc.Z, loc.O, loc.Map, ref manager);
        }
        #endregion

        #region Speed
        [CommandHelp(".speed [0.1 - 10] Optional: {run | swim | all} ")]
        public static void Speed(IWorldManager manager, string[] args)
        {
            if (args.Length < 1)
                return;

            Read(args, 0, out float speed);
            speed = Math.Min(Math.Max(speed, 0.1f), 10f); //Min 0.1 Max 10.0

            string type = (args.Length > 1 ? args[1] : "all").ToLower().Trim();

            var character = manager.Account.ActiveCharacter;
            switch (type)
            {
                case "swim":
                    manager.Send(character.BuildForceSpeed(speed, true));
                    break;
                case "run":
                    manager.Send(character.BuildForceSpeed(speed));
                    break;
                default:
                    manager.Send(character.BuildForceSpeed(speed, true));
                    manager.Send(character.BuildForceSpeed(speed));
                    break;
            }

            manager.Send(character.BuildMessage($"{type.ToUpperFirst()} speed changed to {speed * 100}% of normal"));
        }
        #endregion

        #region Morph
        [CommandHelp(".morph id")]
        public static void Morph(IWorldManager manager, string[] args)
        {
            if (args.Length < 1)
                return;

            if(Read(args, 0, out uint Id))
            {
                var character = manager.Account.ActiveCharacter;
                character.DisplayId = Id;

                manager.Send(character.BuildUpdate());
            }
        }       

        [CommandHelp(".demorph")]
        public static void Demorph(IWorldManager manager, string[] args)
        {
            var character = manager.Account.ActiveCharacter;
            character.Demorph();
            manager.Send(character.BuildUpdate());
        }
        #endregion

        public static void Help(IWorldManager manager, string[] args)
        {
            var character = manager.Account.ActiveCharacter;
            var attrs = typeof(Commands).GetMethods()
                                        .Where(x => x.IsDefined(typeof(CommandHelpAttribute), false))
                                        .SelectMany(x => x.GetCustomAttributes(typeof(CommandHelpAttribute), false) as CommandHelpAttribute[])
                                        .OrderBy(x => x.HelpText);

            if (attrs.Any())
            {
                manager.Send(character.BuildMessage("Commands: "));
                foreach (var attr in attrs)
                    manager.Send(character.BuildMessage("    " + attr.HelpText));
            }
        }

        private static bool Read<T>(string[] args, uint index, out T result)
        {
            if (index < args.Length)
            {
                var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
                if (converter.IsValid(args[index]))
                {
                    result = (T)converter.ConvertFromString(args[index]);
                    return true;
                }
            }

            result = default(T);
            return false;
        }
    }
}