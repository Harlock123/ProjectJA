// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Host.Components.Timeline;

/// <summary>One renderable row on the timeline: a dated issue plus the WBS
/// hierarchy metadata needed to draw the sidebar (depth indent, parent caret).
/// Only dated issues are exposed to the chart; undated issues land in the
/// separate panel below.</summary>
public sealed record TimelineRow(
    Issue Issue,
    string Wbs,
    int Depth,
    bool HasChildren,
    bool IsCollapsed);
