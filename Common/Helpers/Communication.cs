using System.Collections.Immutable;

namespace Common.Helpers
{
    public static class Communication
    {

        public const string START = "start";

        #region Admin Commands

        public const string USER = "changeuser";
        public const string PASSWORD = "changeuser";
        public const string IP = "ipaddress";
        public const string TCP = "tcpport";
        public const string UDP = "udpport";
        public const string DATA = "datapath";
        public const string RESTART = "restart";
        public const string STOP = "stop";

        #endregion

        #region Statuses

        public const string SUCCESS = "success";
        public const string FAILURE = "failure";

        #endregion

        #region User Commands

        public const string CONNECT = "connect";

        #endregion

        #region TABLES

        public const string SUBSERVERS = "subservers";
        public const string ROOMS = "rooms";
        public const string GROUPS = "groups";

        #endregion

        #region Collections

        public static ImmutableArray<string> ADMIN_CONFIG = new ImmutableArray<string> { USER, IP, TCP, UDP, DATA };
        public static ImmutableArray<string> ADMIN_COMMANDS = new ImmutableArray<string> { USER, PASSWORD, RESTART, STOP};
        public static ImmutableArray<string> INFO_COMMANDS = new ImmutableArray<string> { IP, TCP, UDP };
        public static ImmutableArray<string> USER_COMMANDS = new ImmutableArray<string> { CONNECT };
        public static ImmutableArray<string> STATUSES = new ImmutableArray<string> { SUCCESS, FAILURE };
        public static ImmutableArray<string> TABLES = new ImmutableArray<string> { SUBSERVERS, ROOMS, GROUPS };

        #endregion

    }
}
