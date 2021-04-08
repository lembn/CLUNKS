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

        #region Admin

        public const string CHANGE_USER = "changeuser"; //Change admin username
        public const string PASSWORD = "changepassword"; //Change admin password
        public const string IP = "ipaddress";  //Server IP
        public const string TCP = "tcpport"; //Server TCP port
        public const string UDP = "udpport"; //Server UDP port
        public const string DATA = "datapath"; //Path to data folder on the server machine
        public const string RESTART = "restart"; //Restart the server
        public const string STOP = "stop"; //Stop the server

        #endregion

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

        public static ImmutableArray<string> ADMIN_CONFIG = ImmutableArray.Create(new string[] { CHANGE_USER, PASSWORD, IP, TCP, UDP, DATA });
        public static ImmutableArray<string> ADMIN_COMMANDS = ImmutableArray.Create(new string[]  { RESTART, STOP });
        public static ImmutableArray<string> USER_COMMANDS = ImmutableArray.Create(new string[]  { CONNECT, DISCONNECT, LOGIN });
        public static ImmutableArray<string> STATUSES = ImmutableArray.Create(new string[]  { SUCCESS, FAILURE, INCOMPLETE });
        public static ImmutableArray<string> FINAL_STATUSES = ImmutableArray.Create(new string[]  { SUCCESS, FAILURE });

        #endregion

    }
}
