namespace SixAgesAccessibility
{
    /// <summary>
    /// Two-step Enter confirmation for complex commit screens (multi-slot dialogs
    /// like Sacrifice or Reorganize). First Enter announces a summary of the
    /// user's current selection and arms the gate; second Enter commits.
    ///
    /// <para>The gate only holds a "pending" flag — the calling navigator keeps
    /// control of the commit path. Typical use from a navigator's Enter handler:
    /// <code>
    /// if (_gate.RequestOrConfirm(BuildSummary(d)))
    ///     SubmitButton(d.actionButton); // second Enter — commit
    /// // first Enter falls through: summary was spoken, return.
    /// </code>
    /// The gate must be reset by the navigator whenever the user changes the
    /// selection (Space toggle, slider adjust, etc.) so a stale armed state
    /// cannot commit a different selection than the one the user just heard.</para>
    /// </summary>
    public class ConfirmGate
    {
        private bool _isPending;

        /// <summary>True while waiting for the second Enter.</summary>
        public bool IsPending { get { return _isPending; } }

        /// <summary>
        /// First call: arm the gate, announce <paramref name="summary"/> + the
        /// "press Enter again to confirm" hint, return false.
        /// Second call: disarm the gate and return true so the caller commits.
        /// </summary>
        public bool RequestOrConfirm(string summary)
        {
            if (_isPending)
            {
                _isPending = false;
                return true;
            }

            _isPending = true;
            string hint = Loc.Get("Press Enter again to confirm.");
            if (string.IsNullOrEmpty(summary))
                ScreenReader.Say(hint);
            else
                ScreenReader.Say(summary + " " + hint);
            return false;
        }

        /// <summary>Disarm the gate. Call from any handler that changes the user's selection.</summary>
        public void Reset()
        {
            _isPending = false;
        }
    }
}
