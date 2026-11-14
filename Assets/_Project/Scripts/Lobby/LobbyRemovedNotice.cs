using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// Tells a player the host removed them, on a kit plate above the title, because their own
    /// game only knows the line dropped (SteamLobby's status says "lost the connection").
    /// Shows for a few seconds after LobbyRoster.LocalRemovedByHost, then fades. Local only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRemovedNotice : MonoBehaviour
    {
        [SerializeField] UiTokens _tokens;
        [SerializeField] CanvasGroup _group;
        [SerializeField] UiStatusLine _line;

        [Tooltip("How long the notice stays up, in seconds, before it fades.")]
        [Range(2f, 20f)] [SerializeField] float _seconds = 8f;

        const string Text = "The host removed you from the lobby. You can join again from their Steam profile or an invite.";

        float _until = -1f;

        void Awake() => _group.alpha = 0f;
        void OnEnable() => LobbyRoster.LocalRemovedByHost += Show;
        void OnDisable() => LobbyRoster.LocalRemovedByHost -= Show;

        void Show()
        {
            _line.Set(Text, UiStatusKind.Refusal);
            _until = Time.unscaledTime + _seconds;
            UiSound.Play(UiSoundEvent.Notification);
        }

        void Update()
        {
            float target = Time.unscaledTime < _until ? 1f : 0f;
            if (Mathf.Approximately(_group.alpha, target))
                return;
            float seconds = UiMotion.Reduced ? _tokens.ReducedFade : target > 0f ? _tokens.PanelIn : _tokens.PanelOut;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        }
    }
}
