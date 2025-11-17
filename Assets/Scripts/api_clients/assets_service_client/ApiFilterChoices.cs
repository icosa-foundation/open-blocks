using System;
using System.Linq;
namespace com.google.apps.peltzer.client.api_clients.assets_service_client
{
    public static class ChoicesHelper
    {
        public static bool IsValidChoice<T>(string choice) where T : class
        {
            var fieldValues = typeof(T)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToArray();
            return fieldValues.Contains(choice);
        }

        public static string[] GetAllChoices<T>() where T : class
        {
            return typeof(T)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToArray();
        }
    }

    public class CategoryChoices
    {
        public static string
            ANY = "",
            ANIMALS = "ANIMALS",
            ARCHITECTURE = "ARCHITECTURE",
            ART = "ART",
            CULTURE = "CULTURE",
            EVENTS = "EVENTS",
            FOOD = "FOOD",
            HISTORY = "HISTORY",
            HOME = "HOME",
            MISCELLANEOUS = "MISCELLANEOUS",
            NATURE = "NATURE",
            OBJECTS = "OBJECTS",
            PEOPLE = "PEOPLE",
            PLACES = "PLACES",
            SCIENCE = "SCIENCE",
            SPORTS = "SPORTS",
            TECH = "TECH",
            TRANSPORT = "TRANSPORT",
            TRAVEL = "TRAVEL";

        public static string GetFriendlyName(string category)
        {
            return category switch
            {
                "ANY" => "Any",
                "ANIMALS" => "Animals & Pets",
                "ARCHITECTURE" => "Architecture",
                "ART" => "Art",
                "CULTURE" => "Culture & Humanity",
                "EVENTS" => "Current Events",
                "FOOD" => "Food & Drink",
                "HISTORY" => "History",
                "HOME" => "Furniture & Home",
                "MISCELLANEOUS" => "Miscellaneous",
                "NATURE" => "Nature",
                "OBJECTS" => "Objects",
                "PEOPLE" => "People & Characters",
                "PLACES" => "Places & Scenes",
                "SCIENCE" => "Science",
                "SPORTS" => "Sports & Fitness",
                "TECH" => "Tools & Technology",
                "TRANSPORT" => "Transport",
                "TRAVEL" => "Travel & Leisure",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }
    }

    public class LicenseChoices
    {
        public static readonly string
            ANY = "",
            CC0 = "CREATIVE_COMMONS_0",
            REMIXABLE = "REMIXABLE",
            ALL_CC = "ALL_CC",
            CREATIVE_COMMONS_BY = "CREATIVE_COMMONS_BY",
            CREATIVE_COMMONS_BY_NC = "CREATIVE_COMMONS_BY_NC",
            CREATIVE_COMMONS_BY_ND = "CREATIVE_COMMONS_BY_ND",
            ALL_RIGHTS_RESERVED = "ALL_RIGHTS_RESERVED";

        public static string GetFriendlyName(string licence)
        {
            return licence switch
            {
                "ANY" => "Any License",
                "CC0" => "Creative Commons Zero (Public Domain)",
                "CREATIVE_COMMONS_BY" => "Creative Commons Attribution",
                "CREATIVE_COMMONS_BY_NC" => "Creative Commons Attribution, Non-Commercial",
                "CREATIVE_COMMONS_BY_ND" => "Creative Commons Attribution, No Derivatives",
                "REMIXABLE" => "Any Remixable Licence",
                "ALL_CC" => "Any Creative Commons License",
                "ALL_RIGHTS_RESERVED" => "All Rights Reserved",
                _ => throw new ArgumentOutOfRangeException(nameof(licence), licence, null)
            };
        }
    }

    public class OrderByChoices
    {
        public const string
            NEWEST = "NEWEST",  // Same as CREATE_TIME
            OLDEST = "OLDEST",  // Same as -CREATE_TIME
            BEST = "BEST",
            TRIANGLE_COUNT = "TRIANGLE_COUNT",
            LIKED_TIME = "LIKED_TIME",
            CREATE_TIME = "CREATE_TIME",
            UPDATE_TIME = "UPDATE_TIME",
            LIKES = "LIKES",
            DOWNLOADS = "DOWNLOADS",
            DISPLAY_NAME = "DISPLAY_NAME",
            AUTHOR_NAME = "AUTHOR_NAME";

        public static string GetFriendlyName(string orderBy)
        {
            return orderBy switch
            {
                "NEWEST" => "Newest",
                "OLDEST" => "Oldest",
                "BEST" => "Best",
                "TRIANGLE_COUNT" => "Triangle Count",
                "LIKED_TIME" => "Recently Liked",
                "CREATE_TIME" => "Creation Time",
                "UPDATE_TIME" => "Update Time",
                "LIKES" => "Likes",
                "DOWNLOADS" => "Downloads",
                "DISPLAY_NAME" => "Title",
                "AUTHOR_NAME" => "Author",
                _ => throw new ArgumentOutOfRangeException(nameof(orderBy), orderBy, null)
            };
        }
    }

    public class FormatChoices
    {
        public static string
            ANY = "",
            TILT = "TILT",
            NOT_TILT = "-TILT",
            BLOCKS = "BLOCKS",
            NOT_BLOCKS = "-BLOCKS",
            GLTF = "GLTF",
            NOT_GLTF = "-GLTF",
            GLTF1 = "GLTF1",
            NOT_GLTF1 = "-GLTF1",
            GLTF2 = "GLTF2",
            NOT_GLTF2 = "-GLTF2",
            OBJ = "OBJ",
            NOT_OBJ = "-OBJ",
            FBX = "FBX",
            NOT_FBX = "-FBX",
            VOX = "VOX",
            NOT_VOX = "-VOX";
    }

    public class CuratedChoices
    {
        public static string
            ANY = "",
            TRUE = "true",
            FALSE = "false";
    }
}
