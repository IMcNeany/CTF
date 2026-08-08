using UnityEngine;
using UnityEngine.UIElements;
using Blocks.Gameplay.Core;
using System;

[RequireComponent(typeof(PanelRenderer))]
public class CTFHUD : MonoBehaviour
{
    [SerializeField] private InteractionPromptEvent interactionPromptEvent;
    [SerializeField] private FloatEvent interactionProgressEvent;
    private PanelRenderer m_UIPanelRenderer;

    //Interaction
    private Label m_interactionLabel;
    private ProgressBar m_ProgressBar;
    /// <summary>
    /// Initializes the UI system and validates required components.
    /// </summary>

    void OnEnable()
    {
        m_UIPanelRenderer = GetComponent<PanelRenderer>();
        m_UIPanelRenderer.RegisterUIReloadCallback(OnUIReload);
        SetupUICallbacks();
    }

    void OnDisable()
    {
        m_UIPanelRenderer.UnregisterUIReloadCallback(OnUIReload);
        UnregisterUICallbacks();
    }

    void OnUIReload(PanelRenderer renderer, VisualElement root)
    {
        m_ProgressBar = root.Q<ProgressBar>("InteractionProgressBar");
        m_interactionLabel = root.Q<Label>("InteractionText");
        m_ProgressBar.visible = false;
        m_interactionLabel.visible = false;
    }

    private void SetupUICallbacks()
    {
        interactionPromptEvent.RegisterListener(InteractionPromptUpdate);
        interactionProgressEvent.RegisterListener(InteractionProgressUpdate);
    }

    private void UnregisterUICallbacks()
    {
        interactionPromptEvent.UnregisterListener(InteractionPromptUpdate);
        interactionProgressEvent.UnregisterListener(InteractionProgressUpdate);
    }

    private void InteractionPromptUpdate(InteractionPromptState state)
    {
        m_interactionLabel.text = state.Text;
        m_interactionLabel.visible = state.Visible;
        m_ProgressBar.visible = state.ShowProgress;
    }

    private void InteractionProgressUpdate(float progress)
    {
        m_ProgressBar.value = progress;
    }

}
