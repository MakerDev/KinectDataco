using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    internal class SkeletonTemp
    {
        //
        // Summary:
        //     Gets or sets the skeleton's current tracking state.
        public SkeletonTrackingState TrackingState { get; set; }
        //
        // Summary:
        //     Gets or sets the skeleton's tracking ID.
        public int TrackingId { get; set; }
        //
        // Summary:
        //     Gets or sets the skeleton's position.
        public SkeletonPoint Position { get; set; }
        //
        // Summary:
        //     Gets or sets the skeleton's joints.
        public List<Joint> Joints { get; set; } = new List<Joint>();
        //
        // Summary:
        //     Gets or sets the skeleton's bone orientations.
        public List<BoneOrientation> BoneOrientations { get; set; } = new List<BoneOrientation>();
        //
        // Summary:
        //     Gets or sets the edges that this skeleton is clipped on.
        public FrameEdges ClippedEdges { get; set; }
    }

}
