using System.Collections.Immutable;

namespace Common.Helpers
{
    public static class Communication
    {

        public const string START = "start"; //Used for identifying the first stage of a multiple roundtrip communication
        public const string FORWARD = "forward"; //Used for identifying positive ET
        public const string BACKWARD = "backward"; //Used for identifying negative ET 
        public const string TRUE = "true";
        public const string FALSE = "false";
        public const string USER = "user";

        #region Statuses

        public const string SUCCESS = "success"; //Operation was successful
        public const string FAILURE = "failure"; //Operation failed
        public const string INCOMPLETE = "incomplete"; //Operation incomplete

        #endregion

        #region User Commands

        public const string CONNECT = "connect"; //Connect to an entity
        public const string DISCONNECT = "disconnect"; //Disconnect from an entity
        public const string LOGIN = "login"; //Login to user account

        #endregion

        #region Collections

        public static ImmutableArray<string> STATUSES = ImmutableArray.Create(new string[]  { SUCCESS, FAILURE, INCOMPLETE });
        public static ImmutableArray<string> FINAL_STATUSES = ImmutableArray.Create(new string[]  { SUCCESS, FAILURE });

        #endregion

    }
}
