namespace AdaptiveRoads.Manager {
    using ColossalFramework;
    using ColossalFramework.IO;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using KianCommons;
    using System;
    using TrafficManager;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using UnityEngine;
    using Log = KianCommons.Log;
    using System.Linq;
    using AdaptiveRoads.Util;
    using KianCommons.Serialization;
    using KianCommons.Plugins;
    using System.Reflection;
    using AdaptiveRoads.Data;
    using System.Collections.Generic;
    using AdaptiveRoads.Data.NetworkExtensions;

    public struct NetNodeExt {
        public ushort NodeID;
        public Flags m_flags;

        const int CUSTOM_FLAG_SHIFT = 24;
        public bool IsEmpty => (m_flags & Flags.CustomsMask) == Flags.None;
        public void Serialize(SimpleDataSerializer s) => s.WriteInt32(
            ((int)(Flags.CustomsMask & m_flags)) >> CUSTOM_FLAG_SHIFT);
        public void Deserialize(SimpleDataSerializer s) => m_flags =
            m_flags.SetMaskedFlags((Flags)(s.ReadInt32() << CUSTOM_FLAG_SHIFT), Flags.CustomsMask);

        public void Init(ushort nodeID) => NodeID = nodeID;

        [Flags]
        public enum Flags {
            None = 0,
            [Hint(HintExtension.VANILLA)]
            Vanilla = 1 << 0,

            [Hide]
            [Hint("Hide Crossings mod is active")]
            HC_Mod = 1 << 1,

            [Hint("Direct Connect Roads mod is active")]
            DCR_Mod = 1 << 2,

            [Hint("Hide Unconnected Tracks mod is active")]
            HUT_Mod = 1 << 3,

            [Hint("all entering segment ends keep clear of the junction." +
                "useful for drawing pattern on the junction.")]
            KeepClearAll = 1 << 10,

            [Hint("the junction only has two segments.")]
            TwoSegments = 1 << 11,

            [Hint("the junction has segments with different speed limits.")]
            SpeedChange = 1 << 12,

            [CustomFlag] Custom0 = 1 << 24,
            [CustomFlag] Custom1 = 1 << 25,
            [CustomFlag] Custom2 = 1 << 26,
            [CustomFlag] Custom3 = 1 << 27,
            [CustomFlag] Custom4 = 1 << 28,
            [CustomFlag] Custom5 = 1 << 29,
            [CustomFlag] Custom6 = 1 << 30,
            [CustomFlag] Custom7 = 1 << 31,
            CustomsMask = Custom0 | Custom1 | Custom2 | Custom3 | Custom4 | Custom5 | Custom6 | Custom7,
        }

        public static IJunctionRestrictionsManager JRMan =>
            TrafficManager.Constants.ManagerFactory.JunctionRestrictionsManager;

        public void UpdateFlags() {
            m_flags = m_flags.SetFlags(Flags.HC_Mod, NetworkExtensionManager.Instance.HTC);
            m_flags = m_flags.SetFlags(Flags.DCR_Mod, NetworkExtensionManager.Instance.DCR);
            m_flags = m_flags.SetFlags(Flags.HUT_Mod, NetworkExtensionManager.Instance.HUT);

            if (JRMan != null) {
                bool keepClearAll = true;
                foreach(var segmentID in NetUtil.IterateNodeSegments(NodeID)) {
                    bool startNode = NetUtil.IsStartNode(segmentId: segmentID, nodeId: NodeID);
                    bool keppClear = JRMan.IsEnteringBlockedJunctionAllowed(segmentID, startNode);
                    keepClearAll &= keppClear;

                }
                m_flags = m_flags.SetFlags(Flags.KeepClearAll, keepClearAll);


                bool speedChange = TMPEHelpers.SpeedChanges(NodeID);
                bool twoSegments = NodeID.ToNode().CountSegments() == 2;

                m_flags = m_flags.SetFlags(Flags.SpeedChange, speedChange);
                m_flags = m_flags.SetFlags(Flags.TwoSegments, twoSegments);

                GetTrackConnections();
            }
        }

        public override string ToString() {
            return $"NetNodeExt({NodeID} flags={m_flags})";
        }
        #region track
        /* terminology:
         * - connection does not care about source/target.
         * - transition/routing care
         *    - transition is between two lanes.
         *    - routing is a set of transitions.
         */
        public struct Connection {
            public uint LaneID1;
            public uint LaneID2;
            public override bool Equals(object obj) {
                if(obj is Connection rhs) {
                    if(LaneID1 == rhs.LaneID1 && LaneID2 == rhs.LaneID2)
                        return true;
                    if(LaneID1 == rhs.LaneID2 && LaneID2 == rhs.LaneID1)
                        return true;
                }
                return false;
            }
            public override int GetHashCode() => (int)(LaneID1 ^ LaneID2);
        }
        public static HashSet<Connection> tempConnections_ = new HashSet<Connection>();
        public LaneTransition[] Transitions;
        public void GetTrackConnections() {
            if(!NodeID.ToNode().IsValid()) {
                Transitions = null;
                return;
            }
            tempConnections_.Clear();
            foreach(var segmentID in NodeID.ToNode().IterateSegments()) {
                ref var segExt = ref segmentID.ToSegmentExt();
                var infoExt = segExt.NetInfoExt;
                var lanes = segExt.LaneIDs;
                for(int laneIndex = 0; laneIndex < lanes.Length; ++laneIndex) {
                    uint laneID = lanes[laneIndex];
                    foreach(var transtion in TMPEHelpers.GetForwardRoutings(laneID, NodeID)) {
                        var infoExt2 = segmentID.ToSegmentExt().NetInfoExt;
                        if(infoExt.HasTrackLane(laneIndex) || infoExt2.HasTrackLane(transtion.laneIndex)) {
                            tempConnections_.Add(new Connection { LaneID1 = laneID, LaneID2 = transtion.laneId });
                        }
                    }
                }
            }
            Transitions = new LaneTransition[tempConnections_.Count];
            int index = 0;
            foreach(var connection in tempConnections_) {
                var transtion = Transitions[index++];
                transtion.Init(connection.LaneID1, connection.LaneID2); // also calculates
            }
        }

        public void RenderTrackInstance(RenderManager.CameraInfo cameraInfo, int layerMask) {
            if(!NodeID.ToNode().IsValid())
                return;
            if((layerMask & NodeID.ToNode().Info.m_netLayers) == 0)
                return;
            foreach(var transition in Transitions)
                transition.RenderTrackInstance(cameraInfo, layerMask);
        }
        #endregion
    }
}

