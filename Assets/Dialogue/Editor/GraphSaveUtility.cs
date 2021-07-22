using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class GraphSaveUtility
{
    private DialogueGraphView _targetGraphView;
    private DialogueContainer _dialogueContainer;

    private List<Edge> Edges => _targetGraphView.edges.ToList();
    private List<DialogueNode> Nodes => _targetGraphView.nodes.ToList().Cast<DialogueNode>().ToList();

    private const string DataFolder = "Assets/Dialogue/Datas";
    
    public static GraphSaveUtility GetInstance(DialogueGraphView targetGraphView)
    {
        return new GraphSaveUtility
        {
            _targetGraphView = targetGraphView,
        };
    }

    public void SaveGraph(string fileName)
    {
        // if there are no edges(no connections) then return
        if (!Edges.Any())
        {
            return;
        }

        var dialogueContainer = ScriptableObject.CreateInstance<DialogueContainer>();
        var connectedPorts = Edges.Where(x => x.input.node != null).ToArray();
        for (var i = 0; i < connectedPorts.Length; ++i)
        {
            var outputNode = connectedPorts[i].output.node as DialogueNode;
            var inputNode = connectedPorts[i].input.node as DialogueNode;
            
            dialogueContainer.NodeLinks.Add(new NodeLinkData
            {
                BaseNodeGuid = outputNode.GUID,
                PortName = connectedPorts[i].output.portName,
                TargetNodeGuid = inputNode.GUID
            });
        }

        foreach (var dialogueNode in  Nodes.Where(node => !node.EntryPoint))
        {
            dialogueContainer.DialogueNodeDatas.Add(new DialogueNodeData
            {
                Guid = dialogueNode.GUID,
                DialogueText = dialogueNode.DialogueText,
                Position = dialogueNode.GetPosition().position
            });
        }

        if (!AssetDatabase.IsValidFolder(DataFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Dialogue/Datas");
        }
        
        AssetDatabase.CreateAsset(dialogueContainer, GetFilePath(fileName));
        AssetDatabase.SaveAssets();
    }

    public void LoadGraph(string fileName)
    {
        string filePath = GetFilePath(fileName);
        _dialogueContainer = AssetDatabase.LoadAssetAtPath<DialogueContainer>(filePath);
        if (_dialogueContainer == null)
        {
            EditorUtility.DisplayDialog($"File:{filePath} not found", "Target dialogue graph file does not exsits!", "OK");
            return;
        }

        ClearGraph();
        CreateNodes();
        ConnectNodes();
    }

    private void ConnectNodes()
    {
        for (var i = 0; i < Nodes.Count; ++i)
        {
            var connections = _dialogueContainer.NodeLinks.Where(x => x.BaseNodeGuid == Nodes[i].GUID).ToList();
            for (var j = 0; j < connections.Count; ++j)
            {
                var targetNodeGuid = connections[j].TargetNodeGuid;
                var targetNode = Nodes.First(x => x.GUID == targetNodeGuid);
                var output = Nodes[i].outputContainer[j].Q<Port>();
                var input = (Port) targetNode.inputContainer[0];
                LinkNodes(output, input);
                
                targetNode.SetPosition(new Rect(_dialogueContainer.DialogueNodeDatas.First(x => x.Guid == targetNodeGuid).Position,
                    _targetGraphView.DefaultSize));
            }
        }
    }

    private void LinkNodes(Port output, Port input)
    {
        var tempEdge = new Edge
        {
            output = output,
            input = input
        };
        tempEdge.input.Connect(tempEdge);
        tempEdge.output.Connect(tempEdge);
        
        _targetGraphView.Add(tempEdge);
    }

    private void CreateNodes()
    {
        foreach (var nodeData in _dialogueContainer.DialogueNodeDatas)
        {
            var tempNode = _targetGraphView.CreateDialogueNode(nodeData.DialogueText);
            tempNode.GUID = nodeData.Guid;
            _targetGraphView.AddElement(tempNode);

            var nodePorts = _dialogueContainer.NodeLinks.Where(x => x.BaseNodeGuid == nodeData.Guid).ToList();
            Debug.Log($"create node count {nodePorts.Count}");
            nodePorts.ForEach(x => _targetGraphView.AddChoicePort(tempNode, x.PortName));
        }    
    }
    
    private void ClearGraph()
    {
        // Set entry point guid back from the save. Discard existing guid.
        Nodes.Find(x => x.EntryPoint).GUID = _dialogueContainer.NodeLinks[0].BaseNodeGuid;
        
        foreach (var node in Nodes)
        {
            if (node.EntryPoint)
            {
                continue;
            }
            
            // Remove edges that connected to this node
            Edges.Where(x => x.input.node == node).ToList()
                .ForEach(edge => _targetGraphView.RemoveElement(edge));
            
            // Remove the node form the graph
            _targetGraphView.RemoveElement(node);
        }
    }

    private string GetFilePath(string fileName)
    {
        return $"{DataFolder}/{fileName}.asset";
    }
}
