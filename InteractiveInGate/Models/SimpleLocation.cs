using Synchronize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveInGate.Models
{
    class SimpleLocation
    {
        private LocationNode mLocationNode;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private const int MAXIMUM_HIERARCHY_DEPTH = 100;
        public string InteractiveInGateMetaKey { get; set; } = "InteractiveInGateDestination";

        public List<SimpleLocation> Children { get; set; }
        public bool IsLeafNode { get; private set; }
        public bool IsInteractiveInGateDestination { get; private set; }
        public string Name { get; private set; }

        public SimpleLocation(LocationNode node, int recursionDepth = 1)
        {
            mLocationNode = node;
            Name = mLocationNode.Name;

            IsInteractiveInGateDestination = (mLocationNode.Metadata.ContainsKey(InteractiveInGateMetaKey) && mLocationNode.Metadata[InteractiveInGateMetaKey].ToLower() == "true");

            Children = new List<SimpleLocation>();
            if (recursionDepth < MAXIMUM_HIERARCHY_DEPTH)
            {
                foreach (LocationNode childNode in mLocationNode.GetChilds() ?? new List<LocationNode>())
                {
                    Children.Add(new SimpleLocation(childNode, recursionDepth + 1));
                }
            }
            else
            {
                logger.Warn($"Location {Name} ({mLocationNode.Uuid}) reached maximum hierarchy depth of {MAXIMUM_HIERARCHY_DEPTH} and was not allowed to have children.");
                logger.Info("Please check the Radea location structure, there's probably a cyclic inheritance chain.");
            }

            // Map each child to the nearest non-empty generation of InteractiveInGate targets
            List<SimpleLocation> addedChildren = new List<SimpleLocation>();
            List<SimpleLocation> removedChildren = new List<SimpleLocation>();
            ResolveGateLocationChildren(Children, ref addedChildren, ref removedChildren);

            foreach (SimpleLocation added in addedChildren)
            {
                if (!Children.Contains(added))
                    Children.Add(added);
            }
            foreach (SimpleLocation removed in removedChildren)
            {
                if (Children.Contains(removed)) Children.Remove(removed);
            }

            if (Children.Count == 0)
            {
                logger.Debug($"The location {node.Name} was a leaf node.");
                IsLeafNode = true;
            }
            else
            {
                logger.Debug($"The location {node.Name} had {Children.Count} children.");
                IsLeafNode = false;
            }
        }

        public string Describe(int hierarchyDepth = 0)
        {
            string indentation = new string(' ', 2*hierarchyDepth);
            string description = indentation + $"Location {mLocationNode.Name} has {Children.Count} children. Is RGD? {IsInteractiveInGateDestination}" + Environment.NewLine;

            foreach (SimpleLocation child in Children)
            {
                description += child.Describe();
            }

            return description;
        }

        public LocationNode GetLocationNode()
        {
            return mLocationNode;
        }

        /// <summary>
        /// Traverses the location hierarchy of inputChildren and turns next-nearest router gate
        /// destination children into actual children.
        /// </summary>
        /// <remarks>
        /// There may be a gap of several generations between a location that is a router gate destination
        /// and its next (grandgrandgrand...)child that is also a router gate destination. This method
        /// drops the non-destination locations in between and promotes the next nearest generation of
        /// gate destination children into actual children, per original child.
        /// 
        /// Somehow this seems to work, but maybe plan the database structure a bit better next time.
        /// </remarks>
        /// <param name="inputChildren"></param>
        /// <param name="addedChildren">Should be added to inputChildren once this method has run.</param>
        /// <param name="removedChildren">Should be removed from inputChildren once this method has run.</param>
        public void ResolveGateLocationChildren(List<SimpleLocation> inputChildren, ref List<SimpleLocation> addedChildren, ref List<SimpleLocation> removedChildren)
        {
            foreach (SimpleLocation child in inputChildren)
            {
                if (child.IsInteractiveInGateDestination)
                {
                    addedChildren.Add(child);
                }
                else
                {
                    removedChildren.Add(child);
                    ResolveGateLocationChildren(child.Children, ref addedChildren, ref removedChildren);
                }
            }
        }

        /// <summary>
        /// Resolve a chain of locations with a single child each
        /// down to the bottom-level child (leaf node).
        /// </summary>
        /// <returns></returns>
        public LocationNode GetBottomLocationNode()
        {
            if (Children.Count > 1)
            {
                logger.Warn($"The location {Name} has no siblings, but more than one children. It may not be the intended delivery target. Please check the Radea location hierarchy.");
                return mLocationNode;
            }

            if (Children.Count == 1)
            {
                return Children[0].GetBottomLocationNode();
            }
            else
            {
                return mLocationNode;
            }
        }

        /// <summary>
        /// Remove any of the locations that are direct children of other locations in the list.
        /// </summary>
        /// <param name="locations"></param>
        public static void CullDirectChildren(ref List<SimpleLocation> locations)
        {
            List<SimpleLocation> removedLocations = new List<SimpleLocation>();

            foreach (var possibleParent in locations) {
                List<string> listChildUuids = (from SimpleLocation child in possibleParent.Children
                                               select child.GetLocationNode().Uuid).ToList();

                foreach (var possibleChild in locations)
                {
                    if (listChildUuids.Contains(possibleChild.GetLocationNode().Uuid))
                    {
                        removedLocations.Add(possibleChild);
                    }
                }
            }

            foreach (var removed in removedLocations)
            {
                if (locations.Contains(removed)) locations.Remove(removed);
            }
        }
    }
}
