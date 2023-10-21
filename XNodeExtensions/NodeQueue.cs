﻿using Cysharp.Threading.Tasks;
using SiphoinUnityHelpers.XNodeExtensions.AsyncNodes;
using SiphoinUnityHelpers.XNodeExtensions.Exceptions;
using SiphoinUnityHelpers.XNodeExtensions.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static UnityEditor.Progress;

namespace SiphoinUnityHelpers.XNodeExtensions
{
    public class NodeQueue
    {
        private int _index;

        private StartNode _startNode;

        public event Action OnEnd;

        private List<BaseNodeInteraction> _nodes;

        private List<AsyncNode> _asyncNodes;

        private List<ExitNode> _exitNodes;

        private BaseGraph _graph;

        public int Count => _nodes.Count;

        public BaseNode Current => _nodes[_index];

        public bool IsEnding => _index == Count;


        public NodeQueue(BaseGraph parentGraph, IEnumerable<BaseNodeInteraction> nodes)
        {
            if (parentGraph is null)
            {
                throw new ArgumentNullException("parent graph is null");
            }

            if (nodes is null)
            {
                throw new ArgumentNullException("nodes is null");
            }

            _nodes = new List<BaseNodeInteraction>();

            _asyncNodes = new List<AsyncNode>();

            _exitNodes = new List<ExitNode>();

            _graph = parentGraph;

            ValidateGraph(nodes);

            Build(nodes);
        }

        private void ValidateGraph(IEnumerable<BaseNodeInteraction> nodes)
        {
            Func<BaseNodeInteraction, bool> predicateFindStartNode = x => x is StartNode && x.Enabled && x.GetEnterPort().Connection != null;

            Func<BaseNodeInteraction, bool> predicateFindExitNode = x => x is ExitNode && x.Enabled && x.GetEnterPort().Connection != null;

            if (nodes.Count(predicateFindExitNode) == 0)
            {
                throw new NodeQueueException($"graph {_graph.name} not have Exit Nodes!");
            }

            if (nodes.Count(predicateFindStartNode) > 1)
            {
                throw new NodeQueueException($"graph {_graph.name} has more 1 Start Node!");
            }
            try
            {
                _startNode = nodes.Single(predicateFindStartNode) as StartNode;
            }
            catch
            {
                throw new NodeQueueException($"Start Node not found on graph {_graph.name}");
            }
        }

        private void Build(IEnumerable<BaseNodeInteraction> nodes)
        {
            foreach (var item in nodes)
            {
                if (item is AsyncNode)
                {
                    _asyncNodes.Add(item as AsyncNode);
                }

                if (item is ExitNode)
                {
                    ExitNode exitNode = item as ExitNode;

                    _exitNodes.Add(item as ExitNode);

                    exitNode.OnExit += OnExit;

                }
            }
            _nodes.Add(_startNode);

            var currentNode = _startNode as BaseNodeInteraction;

            while (currentNode != null)
            {
                var exitPort = currentNode.GetExitPort();

                if (exitPort.Connection != null)
                {
                    var nextNode = exitPort.Connection.node as BaseNodeInteraction;
                    if (nextNode.Enabled)
                    {
                        _nodes.Add(nextNode);
                    }

                    currentNode = nextNode;
                }
                else
                {
                    currentNode = null;
                }
            }

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"New Node Queue from node graph {_graph.name}:\n");

            foreach (var node in _nodes)
            {
                stringBuilder.AppendLine(node.name);
            }

            Debug.Log(stringBuilder.ToString());
        }

        private void OnExit(object sender, EventArgs e)
        {
            var node = sender as ExitNode;

            node.OnExit -= OnExit;

            Exit();
        }

        private void Exit()
        {
            StopAsyncNodes();

            Debug.Log($"node queue from graph {_graph.name} finished");

            OnEnd?.Invoke();
        }

        public async UniTask<BaseNode> Next ()
        {
            int currentIndex = _index;

            var node = _nodes[currentIndex];



            if (node.Enabled)
            {
                node.Execute();

                if (node is AsyncNode)
                {
                    var asyncNode = node as AsyncNode;

                    Debug.Log($"Wait node {asyncNode.name} GUID; {asyncNode.GUID}");

                    await XNodeExtensionsUniTask.WaitAsyncNode(asyncNode);
                }



                if (_index != Count)
                {
                    _index = Mathf.Clamp(_index + 1, 0, _nodes.Count - 1);
                }

                else
                {
                    return null;
                }

            }

            return node;
        }

        private void StopAsyncNodes()
        {
            foreach (var item in _asyncNodes)
            {
                item.StopTask();
            }
        }
    }
}
