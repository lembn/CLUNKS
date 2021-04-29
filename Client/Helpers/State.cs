namespace Client.Helpers
{
    /// <summary>
    /// A reference type wrapper for the bool class
    /// </summary>
    public class State
    {
        private bool state;

        public State(bool initial) => state = initial;

        public static implicit operator bool(State a) => a.state;
        public static implicit operator State(bool a) => new State(a);
    }
}
