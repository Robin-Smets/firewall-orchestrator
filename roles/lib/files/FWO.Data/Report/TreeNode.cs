using FWO.Basics;

namespace FWO.Data.Report
{
    public class TreeNode<T>
    {
        public T? Item { get; set; }
        public bool IsExpanded { get; set; } = false;
        public List<TreeNode<T>> Children { get; set; } = [];
        public RulebaseType Type { get; set; }
        public bool IsCollapsible { get; set; } = false;
        public string Header { get; set; } = "";

        public TreeNode()
        {

        }

        public TreeNode(ITreeItem<T> treeItem)
        {
            Item = treeItem.Data;
        }
    }
}
