using UnityEngine;

namespace CyclopsScannerModule.Behaviours;

/// <summary>
/// Invisible in-world interaction box that lets the player open the Cyclops Scanner menu by
/// looking at it and pressing the interact button, controller/Steam-Deck friendly alternative
/// to the keybind in <see cref="CyclopsScannerController.Update"/>. Created and positioned by
/// <see cref="CyclopsScannerController.CreateHandTarget"/>; the reticle raycast finds this
/// component by walking up from the hit trigger collider on the same GameObject.
/// </summary>
public class ScannerHandTarget : MonoBehaviour, IHandTarget
{
    public CyclopsScannerController Owner;

    public void OnHandHover(GUIHand hand)
    {
        if (Owner == null || !Owner.ModuleInstalled) return;
        if (HandReticle.main == null) return;

        HandReticle.main.SetIcon(HandReticle.IconType.Hand, 1f);
        HandReticle.main.SetText(HandReticle.TextType.Hand, "Cyclops Scanner", false, GameInput.Button.LeftHand);

        string subscript = Owner.ScanActive && Owner.SelectedType != TechType.None
            ? "Scanning: " + DisplayName(Owner.SelectedType)
            : "Open scanner menu";
        HandReticle.main.SetText(HandReticle.TextType.HandSubscript, subscript, false, GameInput.Button.None);
    }

    public void OnHandClick(GUIHand hand)
    {
        if (Owner == null || !Owner.ModuleInstalled) return;
        UI.ScannerMenu.Toggle(Owner);
    }

    private static string DisplayName(TechType techType)
    {
        return Language.main != null ? Language.main.Get(techType.AsString(false)) : techType.ToString();
    }
}
