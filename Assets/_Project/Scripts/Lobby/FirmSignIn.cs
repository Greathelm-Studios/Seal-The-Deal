using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// A firm's sign-in stand. Press interact to sign in with that firm; the host checks the
    /// fairness rule and either moves you or refuses (the secretary says which).
    /// </summary>
    public sealed class FirmSignIn : LobbyInteractable
    {
        [Tooltip("Which firm this stand signs players in with: 0 = the first firm, 1 = the second.")]
        [Range(0, 1)] [SerializeField] int _firm;

        [Tooltip("The firm names and colours.")]
        [SerializeField] LobbyRules _rules;

        [Tooltip("The sheet on the stand that slides when someone signs. Optional; a child made for this.")]
        [SerializeField] Transform _sheet;

        string _keyPrompt;
        string _padPrompt;
        string _signedPrompt;
        float _slideTime = -1f;
        Vector3 _sheetRest;

        const float SlideSeconds = 0.2f;
        const float SlideDistance = 0.04f;

        public int Firm => _firm;

        void Awake()
        {
            // Built once: the firm name is fixed for the session.
            string name = _rules.FirmName(_firm);
            _keyPrompt = "F   Sign in with " + name;
            _padPrompt = "Y   Sign in with " + name;
            _signedPrompt = "Signed in with " + name;
            if (_sheet != null)
                _sheetRest = _sheet.localPosition;
        }

        public override string Prompt(bool gamepad)
        {
            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == null || roster.RunInProgress || !roster.TryGetLocal(out LobbyMember me) || me.State == MemberState.Fitting)
                return null;
            if (me.Firm == _firm)
                return _signedPrompt;
            return gamepad ? _padPrompt : _keyPrompt;
        }

        public override void OnPress()
        {
            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == null || !roster.TryGetLocal(out LobbyMember me) || me.Firm == _firm)
                return;

            roster.RequestFirm(_firm);
            if (_sheet != null)
                _slideTime = 0f;
        }

        void Update()
        {
            if (_slideTime < 0f)
                return;

            _slideTime += Time.deltaTime;
            float t = Mathf.Clamp01(_slideTime / SlideSeconds);
            _sheet.localPosition = _sheetRest + Vector3.forward * (SlideDistance * Mathf.Sin(t * Mathf.PI));
            if (t >= 1f)
            {
                _slideTime = -1f;
                _sheet.localPosition = _sheetRest;
            }
        }
    }
}
