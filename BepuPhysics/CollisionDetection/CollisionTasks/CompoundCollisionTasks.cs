﻿using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BepuPhysics.CollisionDetection.CollisionTasks
{

    public class ConvexCompoundCollisionTask<TConvex> : CollisionTask where TConvex : struct, IConvexShape
    {
        public ConvexCompoundCollisionTask()
        {
            BatchSize = 8;
            ShapeTypeIndexA = default(TConvex).TypeId;
            ShapeTypeIndexB = default(Compound).TypeId;
            SubtaskGenerator = true;
        }

        public unsafe override void ExecuteBatch<TCallbacks>(ref UntypedList batch, ref CollisionBatcher<TCallbacks> batcher)
        {
            var testPairs = batch.Buffer.As<TestPair<TConvex, Compound>>();
            for (int i = 0; i < batch.Count; ++i)
            {
                ref var pair = ref Buffer<TestPair<TConvex, Compound>>.Get(ref batch.Buffer, i);
                Debug.Assert(pair.Shared.Continuation.ChildA == 0 && pair.Shared.Continuation.ChildB == 0 && pair.Shared.Continuation.Type == CollisionContinuationType.Direct,
                    "Compound-involving pairs cannot be marked as children of compound pairs. Convex-convex children of such pairs will be.");
                ref var continuation = ref batcher.NonconvexReductions.CreateContinuation(pair.B.Children.Length, batcher.Pool, out var continuationIndex);
                for (int j = 0; j < pair.B.Children.Length; ++j)
                {
                    //Note that we have to take into account whether we flipped the shapes to match the expected memory layout.
                    //The caller expects results according to the submitted pair order, not the batcher's memory layout order.
                    int childA, childB;
                    if (pair.Shared.FlipMask < 0)
                    {
                        childA = j;
                        childB = 0;
                    }
                    else
                    {
                        childA = 0;
                        childB = j;
                    }
                    if (batcher.Callbacks.AllowCollisionTesting(pair.Shared.Continuation.PairId, childA, childB))
                    {
                        ref var child = ref pair.B.Children[j];
                        Compound.GetWorldPose(ref child.LocalPose, ref pair.Shared.PoseB, out var childWorldPose);
                        var childShapeType = child.ShapeIndex.Type;
                        batcher.Shapes[childShapeType].GetShapeData(child.ShapeIndex.Index, out var childShapePointer, out var childShapeSize);

                        //Note that we can safely take a pointer to the pair-stored shape:
                        //1) It's stored in a buffer, which is guaranteed GC safe
                        //2) The data contained is copied by the time Add returns, so there's no concern about invalid pointers getting stored.
                        var continuationInfo = new PairContinuation(pair.Shared.Continuation.PairId, childA, childB, CollisionContinuationType.NonconvexReduction, continuationIndex);
                        batcher.Add(pair.A.TypeId, child.ShapeIndex.Type, Unsafe.SizeOf<TConvex>(), childShapeSize, Unsafe.AsPointer(ref pair.A), childShapePointer,
                            ref pair.Shared.PoseA, ref childWorldPose, ref continuationInfo);
                    }
                    else
                    {
                        continuation.OnChildCompletedEmpty(ref pair.Shared.Continuation, ref batcher);
                    }
                }

            }
        }
    }
}
