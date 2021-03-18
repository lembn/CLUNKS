using System.Collections.Immutable;

namespace Common.Helpers
{
    public static class Communication
    {

        public const string START = "start";
        public const string BACKWARD = "backward";
        public const string FORWARD = "forward";

        #region Admin

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
        public const string INCOMPLETE = "incomplete";
        public const string COMPLETE = "complete";

        #endregion

        #region User Commands

        public const string CONNECT = "connect";
        public const string DISCONNECT = "disconnect";

        #endregion

        #region Collections

        public static ImmutableArray<string> ADMIN_CONFIG = ImmutableArray.Create(new string[] { USER, PASSWORD, IP, TCP, UDP, DATA });
        public static ImmutableArray<string> ADMIN_COMMANDS = ImmutableArray.Create(new string[]  { RESTART, STOP});
        public static ImmutableArray<string> USER_COMMANDS = ImmutableArray.Create(new string[]  { CONNECT, DISCONNECT });
        public static ImmutableArray<string> STATUSES = ImmutableArray.Create(new string[]  { SUCCESS, FAILURE, INCOMPLETE, COMPLETE });

        #endregion

    }
}
