// SPDX-FileCopyrightText: 2022 Moony
// SPDX-FileCopyrightText: 2022 Vera Aguilera Puerto
// SPDX-FileCopyrightText: 2022 mirrorcult
// SPDX-FileCopyrightText: 2022 rolfero
// SPDX-FileCopyrightText: 2022 wrexbe
// SPDX-FileCopyrightText: 2023 DrSmugleaf
// SPDX-FileCopyrightText: 2023 Leon Friedrich
// SPDX-FileCopyrightText: 2023 chromiumboy
// SPDX-FileCopyrightText: 2023 keronshb
// SPDX-FileCopyrightText: 2023 metalgearsloth
// SPDX-FileCopyrightText: 2024 IProduceWidgets
// SPDX-FileCopyrightText: 2024 Tayrtahn
// SPDX-FileCopyrightText: 2024 nikthechampiongr
// SPDX-FileCopyrightText: 2025 J
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.Electrocution;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Map;
using CableCuttingFinishedEvent = Content.Shared.Tools.Systems.CableCuttingFinishedEvent;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;

namespace Content.Server.Power.EntitySystems;

public sealed partial class CableSystem : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileManager = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocutionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeCablePlacer();

        SubscribeLocalEvent<CableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CableComponent, CableCuttingFinishedEvent>(OnCableCut);
        // Shouldn't need re-anchoring.
        SubscribeLocalEvent<CableComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnInteractUsing(EntityUid uid, CableComponent cable, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (cable.CuttingQuality != null)
        {
            args.Handled = _toolSystem.UseTool(args.Used, args.User, uid, cable.CuttingDelay, cable.CuttingQuality, new CableCuttingFinishedEvent());
        }
    }

    private void OnCableCut(EntityUid uid, CableComponent cable, DoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var xform = Transform(uid);
        var ev = new CableAnchorStateChangedEvent(xform);
        RaiseLocalEvent(uid, ref ev);

        if (_electrocutionSystem.TryDoElectrifiedAct(uid, args.User))
            return;

        _adminLogger.Add(LogType.CableCut, LogImpact.Medium, $"The {ToPrettyString(uid)} at {xform.Coordinates} was cut by {ToPrettyString(args.User)}.");

        Spawn(cable.CableDroppedOnCutPrototype, xform.Coordinates);
        QueueDel(uid);
    }

    private void OnAnchorChanged(EntityUid uid, CableComponent cable, ref AnchorStateChangedEvent args)
    {
        var ev = new CableAnchorStateChangedEvent(args.Transform, args.Detaching);
        RaiseLocalEvent(uid, ref ev);

        if (args.Anchored)
            return; // huh? it wasn't anchored?

        // anchor state can change as a result of deletion (detach to null).
        // We don't want to spawn an entity when deleted.
        if (TerminatingOrDeleted(uid))
            return;

        // This entity should not be un-anchorable. But this can happen if the grid-tile is deleted (RCD, explosion,
        // etc). In that case: behave as if the cable had been cut.
        Spawn(cable.CableDroppedOnCutPrototype, Transform(uid).Coordinates);
        QueueDel(uid);
    }
}
