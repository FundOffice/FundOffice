using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FMO;


public partial class EquityStructureDiagram : UserControl
{
    // 依赖属性定义
    public static readonly DependencyProperty NodesProperty =
        DependencyProperty.Register("Nodes", typeof(ObservableCollection<EquityNode>),
            typeof(EquityStructureDiagram), new PropertyMetadata(null, OnNodesChanged));

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register("LineBrush", typeof(Brush),
            typeof(EquityStructureDiagram), new PropertyMetadata(Brushes.Black));

    public static readonly DependencyProperty NodeBackgroundProperty =
        DependencyProperty.Register("NodeBackground", typeof(Brush),
            typeof(EquityStructureDiagram), new PropertyMetadata(Brushes.LightBlue));

    public static readonly DependencyProperty NodeBorderBrushProperty =
        DependencyProperty.Register("NodeBorderBrush", typeof(Brush),
            typeof(EquityStructureDiagram), new PropertyMetadata(Brushes.Blue));

    public static readonly DependencyProperty NodeBorderThicknessProperty =
        DependencyProperty.Register("NodeBorderThickness", typeof(double),
            typeof(EquityStructureDiagram), new PropertyMetadata(1.0));

    public static readonly DependencyProperty NodeWidthProperty =
        DependencyProperty.Register("NodeWidth", typeof(double),
            typeof(EquityStructureDiagram), new PropertyMetadata(120.0));

    public static readonly DependencyProperty NodeHeightProperty =
        DependencyProperty.Register("NodeHeight", typeof(double),
            typeof(EquityStructureDiagram), new PropertyMetadata(60.0));

    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register("HorizontalSpacing", typeof(double),
            typeof(EquityStructureDiagram), new PropertyMetadata(80.0));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register("VerticalSpacing", typeof(double),
            typeof(EquityStructureDiagram), new PropertyMetadata(100.0));

    public static readonly DependencyProperty TextBrushProperty =
        DependencyProperty.Register("TextBrush", typeof(Brush),
            typeof(EquityStructureDiagram), new PropertyMetadata(Brushes.Black));


    public static readonly DependencyProperty LineThicknessProperty =
        DependencyProperty.Register("LineThickness", typeof(double),
            typeof(EquityStructureDiagram), new PropertyMetadata(1.0));

    public static readonly DependencyProperty CompanyNodeBackgroundProperty =
        DependencyProperty.Register("CompanyNodeBackground", typeof(Brush),
            typeof(EquityStructureDiagram), new PropertyMetadata(Brushes.LightBlue));

    public static readonly DependencyProperty NaturalPersonBackgroundProperty =
        DependencyProperty.Register("NaturalPersonBackground", typeof(Brush),
            typeof(EquityStructureDiagram), new PropertyMetadata(Brushes.LightGreen));

    // 属性包装器
    public ObservableCollection<EquityNode> Nodes
    {
        get { return (ObservableCollection<EquityNode>)GetValue(NodesProperty); }
        set { SetValue(NodesProperty, value); }
    }

    public Brush LineBrush
    {
        get { return (Brush)GetValue(LineBrushProperty); }
        set { SetValue(LineBrushProperty, value); }
    }

    public Brush NodeBackground
    {
        get { return (Brush)GetValue(NodeBackgroundProperty); }
        set { SetValue(NodeBackgroundProperty, value); }
    }

    public Brush NodeBorderBrush
    {
        get { return (Brush)GetValue(NodeBorderBrushProperty); }
        set { SetValue(NodeBorderBrushProperty, value); }
    }

    public double NodeBorderThickness
    {
        get { return (double)GetValue(NodeBorderThicknessProperty); }
        set { SetValue(NodeBorderThicknessProperty, value); }
    }

    public double NodeWidth
    {
        get { return (double)GetValue(NodeWidthProperty); }
        set { SetValue(NodeWidthProperty, value); }
    }

    public double NodeHeight
    {
        get { return (double)GetValue(NodeHeightProperty); }
        set { SetValue(NodeHeightProperty, value); }
    }

    public double HorizontalSpacing
    {
        get { return (double)GetValue(HorizontalSpacingProperty); }
        set { SetValue(HorizontalSpacingProperty, value); }
    }

    public double VerticalSpacing
    {
        get { return (double)GetValue(VerticalSpacingProperty); }
        set { SetValue(VerticalSpacingProperty, value); }
    }

    public Brush TextBrush
    {
        get { return (Brush)GetValue(TextBrushProperty); }
        set { SetValue(TextBrushProperty, value); }
    }


    public double LineThickness
    {
        get { return (double)GetValue(LineThicknessProperty); }
        set { SetValue(LineThicknessProperty, value); }
    }

    public Brush CompanyNodeBackground
    {
        get { return (Brush)GetValue(CompanyNodeBackgroundProperty); }
        set { SetValue(CompanyNodeBackgroundProperty, value); }
    }

    public Brush NaturalPersonBackground
    {
        get { return (Brush)GetValue(NaturalPersonBackgroundProperty); }
        set { SetValue(NaturalPersonBackgroundProperty, value); }
    }

    // 节点控件字典
    private Dictionary<EquityNode, ContentControl> nodeControls = new Dictionary<EquityNode, ContentControl>();
    private List<Line> relationshipLines = new List<Line>();
    private List<TextBlock> percentageLabels = new List<TextBlock>();

    public EquityStructureDiagram()
    {
        InitializeComponent();
        Loaded += EquityStructureDiagram_Loaded;
    }

    private void EquityStructureDiagram_Loaded(object sender, RoutedEventArgs e)
    {
        RenderDiagram();
    }

    private static void OnNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var diagram = d as EquityStructureDiagram;
        diagram?.RenderDiagram();
    }

    private void RenderDiagram()
    {
        DiagramCanvas.Children.Clear();
        nodeControls.Clear();
        relationshipLines.Clear();
        percentageLabels.Clear();

        if (Nodes == null || Nodes.Count == 0)
            return;

        // 计算布局
        double startX = DiagramCanvas.ActualWidth / 2;
        double startY = 50;

        // 计算每个根节点的水平分布
        double totalWidth = Nodes.Count * NodeWidth + (Nodes.Count - 1) * HorizontalSpacing;
        startX -= totalWidth / 2;

        // 渲染每个根节点及其子树
        for (int i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            double x = startX + i * (NodeWidth + HorizontalSpacing);
            node.Position = new Point(x, startY);

            RenderNode(node, 0);
        }

        // 渲染连接线
        RenderRelationships();
    }

    private void RenderNode(EquityNode node, int level)
    {
        if (node == null)
            return;

        // 创建节点控件
        var nodeControl = new ContentControl
        {
            Content = node,
            ContentTemplateSelector = (DataTemplateSelector)DiagramCanvas.FindResource("NodeTemplateSelector"),
            Width = NodeWidth,
            Height = NodeHeight
        };

        // 设置节点位置
        Canvas.SetLeft(nodeControl, node.Position.X);
        Canvas.SetTop(nodeControl, node.Position.Y);

        // 添加到画布
        DiagramCanvas.Children.Add(nodeControl);
        nodeControls[node] = nodeControl;

        // 计算子节点位置
        if (node.IsExpanded && node.Children != null && node.Children.Count > 0)
        {
            // 计算子节点总宽度
            int visibleChildrenCount = node.Children.Count(c => c.ChildNode != null);
            double totalChildrenWidth = visibleChildrenCount * NodeWidth +
                (visibleChildrenCount - 1) * HorizontalSpacing;

            // 子节点起始X坐标，居中对齐
            double startX = node.Position.X + NodeWidth / 2 - totalChildrenWidth / 2;
            double nextLevelY = node.Position.Y + NodeHeight + VerticalSpacing;

            // 渲染子节点
            double currentX = startX;
            foreach (var relation in node.Children)
            {
                if (relation.ChildNode == null)
                    continue;

                relation.ChildNode.Position = new Point(currentX, nextLevelY);
                currentX += NodeWidth + HorizontalSpacing;

                // 递归渲染子树
                RenderNode(relation.ChildNode, level + 1);
            }
        }
    }

    private void RenderRelationships()
    {
        if (Nodes == null)
            return;

        // 使用队列进行广度优先遍历
        var queue = new Queue<EquityNode>();
        foreach (var rootNode in Nodes)
        {
            queue.Enqueue(rootNode);
        }

        while (queue.Count > 0)
        {
            var parentNode = queue.Dequeue();

            if (parentNode.IsExpanded && parentNode.Children != null)
            {
                foreach (var relation in parentNode.Children)
                {
                    if (relation.ChildNode == null)
                        continue;

                    // 渲染连接线
                    RenderRelationshipLine(parentNode, relation.ChildNode, relation.OwnershipPercentage);

                    // 将子节点加入队列
                    queue.Enqueue(relation.ChildNode);
                }
            }
        }
    }

    private void RenderRelationshipLine(EquityNode parent, EquityNode child, double percentage)
    {
        if (!nodeControls.ContainsKey(parent) || !nodeControls.ContainsKey(child))
            return;

        // 计算连接线的起点和终点
        double startX = parent.Position.X + NodeWidth / 2;
        double startY = parent.Position.Y + NodeHeight;
        double endX = child.Position.X + NodeWidth / 2;
        double endY = child.Position.Y;

        // 创建连接线
        var line = new Line
        {
            X1 = startX,
            Y1 = startY,
            X2 = endX,
            Y2 = endY,
            Stroke = LineBrush,
            StrokeThickness = LineThickness,
            StrokeEndLineCap = PenLineCap.Triangle
        };

        DiagramCanvas.Children.Add(line);
        relationshipLines.Add(line);

        // 创建百分比标签
        if (percentage > 0)
        {
            var label = new TextBlock
            {
                Text = $"{percentage:P}",
                Foreground = TextBrush,
                FontSize = FontSize,
                Background = Brushes.White,
                Padding = new Thickness(2, 1, 2, 1)
            };

            // 标签位置在连接线的中间
            double labelX = (startX + endX) / 2 - label.Width / 2;
            double labelY = (startY + endY) / 2 - label.Height / 2;

            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, labelY);

            DiagramCanvas.Children.Add(label);
            percentageLabels.Add(label);
        }
    }
}

// 节点类型模板选择器
public class NodeTypeTemplateSelector : DataTemplateSelector
{
    public DataTemplate CompanyTemplate { get; set; }
    public DataTemplate NaturalPersonTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is CompanyNode)
            return CompanyTemplate;
        else if (item is NaturalPersonNode)
            return NaturalPersonTemplate;

        return base.SelectTemplate(item, container);
    }
}

// 股权节点基类
public abstract partial class EquityNode : ObservableObject
{
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private Point position;

    [ObservableProperty]
    private double width;

    [ObservableProperty]
    private double height;

    public ObservableCollection<OwnershipRelation> Children { get; set; } = new ObservableCollection<OwnershipRelation>();
}

// 公司股东节点
public partial class CompanyNode : EquityNode
{
    [ObservableProperty]
    private string? businessLicense;

    [ObservableProperty]
    private string? legalRepresentative;

    public override string ToString()
    {
        return $"公司: {Name}";
    }
}

// 自然人股东节点
public partial class NaturalPersonNode : EquityNode
{
    [ObservableProperty]
    private string? idNumber;

    [ObservableProperty]
    private DateTime birthDate;

    public override string ToString()
    {
        return $"自然人: {Name}";
    }
}

// 股权关系类
public partial class OwnershipRelation : ObservableObject
{
    [ObservableProperty]
    private double ownershipPercentage;

    [ObservableProperty]
    private EquityNode childNode;
}

// 股权结构视图模型
public partial class EquityViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<EquityNode> companyNodes;

    [ObservableProperty]
    private EquityNode selectedNode;

    public ICommand AddCompanyCommand { get; }
    public ICommand DeleteCompanyCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }

    public EquityViewModel()
    {
        CompanyNodes = new ObservableCollection<EquityNode>();
        InitializeSampleData();

        AddCompanyCommand = new RelayCommand(AddCompany);
        DeleteCompanyCommand = new RelayCommand(DeleteCompany, CanDeleteCompany);
        ExpandAllCommand = new RelayCommand(ExpandAll);
        CollapseAllCommand = new RelayCommand(CollapseAll);
    }

    private void InitializeSampleData()
    {
        // 创建公司节点
        var parentCompany = new CompanyNode
        {
            Name = "母公司",
            BusinessLicense = "123456789",
            LegalRepresentative = "张三"
        };

        var subsidiary1 = new CompanyNode
        {
            Name = "子公司1",
            BusinessLicense = "111111111",
            LegalRepresentative = "李四"
        };

        var subsidiary2 = new CompanyNode
        {
            Name = "子公司2",
            BusinessLicense = "222222222",
            LegalRepresentative = "王五"
        };

        // 创建自然人股东
        var person1 = new NaturalPersonNode
        {
            Name = "赵六",
            IdNumber = "110101198001011234",
            BirthDate = new DateTime(1980, 1, 1)
        };

        var person2 = new NaturalPersonNode
        {
            Name = "钱七",
            IdNumber = "110101198505055678",
            BirthDate = new DateTime(1985, 5, 5)
        };

        // 设置股权关系
        parentCompany.Children.Add(new OwnershipRelation { ChildNode = subsidiary1, OwnershipPercentage = 0.7 });
        parentCompany.Children.Add(new OwnershipRelation { ChildNode = subsidiary2, OwnershipPercentage = 0.6 });
        parentCompany.Children.Add(new OwnershipRelation { ChildNode = person1, OwnershipPercentage = 0.15 });

        subsidiary1.Children.Add(new OwnershipRelation { ChildNode = person2, OwnershipPercentage = 0.3 });
        subsidiary1.Children.Add(new OwnershipRelation { ChildNode = subsidiary2, OwnershipPercentage = 0.2 });

        person1.Children.Add(new OwnershipRelation { ChildNode = subsidiary2, OwnershipPercentage = 0.1 });

        // 添加到节点集合
        CompanyNodes.Add(parentCompany);
        CompanyNodes.Add(subsidiary1);
        CompanyNodes.Add(subsidiary2);
        CompanyNodes.Add(person1);
        CompanyNodes.Add(person2);
    }

    private void AddCompany()
    {
        var newCompany = new CompanyNode
        {
            Name = "新公司",
            BusinessLicense = "987654321",
            LegalRepresentative = "新法人"
        };

        CompanyNodes.Add(newCompany);
    }

    private void DeleteCompany()
    {
        if (SelectedNode != null)
        {
            // 从父节点的子节点集合中移除
            foreach (var node in CompanyNodes)
            {
                RemoveNodeFromChildren(node, SelectedNode);
            }

            // 从根节点集合中移除
            CompanyNodes.Remove(SelectedNode);
        }
    }

    private void RemoveNodeFromChildren(EquityNode parent, EquityNode nodeToRemove)
    {
        if (parent.Children == null)
            return;

        parent.Children.Remove(parent.Children.FirstOrDefault(c => c.ChildNode == nodeToRemove));

        foreach (var childRelation in parent.Children)
        {
            RemoveNodeFromChildren(childRelation.ChildNode, nodeToRemove);
        }
    }

    private bool CanDeleteCompany()
    {
        return SelectedNode != null;
    }

    private void ExpandAll()
    {
        foreach (var node in CompanyNodes)
        {
            ExpandNode(node);
        }
    }

    private void ExpandNode(EquityNode node)
    {
        if (node == null)
            return;

        node.IsExpanded = true;

        if (node.Children != null)
        {
            foreach (var childRelation in node.Children)
            {
                ExpandNode(childRelation.ChildNode);
            }
        }
    }

    private void CollapseAll()
    {
        foreach (var node in CompanyNodes)
        {
            CollapseNode(node);
        }
    }

    private void CollapseNode(EquityNode node)
    {
        if (node == null)
            return;

        node.IsExpanded = false;

        if (node.Children != null)
        {
            foreach (var childRelation in node.Children)
            {
                CollapseNode(childRelation.ChildNode);
            }
        }
    }
}