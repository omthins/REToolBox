using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Text.RegularExpressions;
// 引入 ContentDialog 命名空间
using Microsoft.UI.Xaml.Controls;

namespace REToolBox.Views
{
    public sealed partial class MathPage : Page
    {
        // 存储所有已绘制函数的点集
        private List<List<Point>> allGraphs = new List<List<Point>>();
        // 画布尺寸
        private double canvasWidth = 800;
        private double canvasHeight = 600;
        // 坐标轴范围
        private double xAxisMin = -10;
        private double xAxisMax = 10;
        private double yAxisMin = -10;
        private double yAxisMax = 10;
        // 网格步数常量
        private const int GridStepCount = 10;

        public MathPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// 处理“绘制”按钮点击事件。
        /// </summary>
        private async void DrawButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取用户输入的函数表达式
            string functionExpression = FunctionInput.Text.Trim();
            if (string.IsNullOrEmpty(functionExpression))
            {
                // 输入为空时，显示弹窗提示
                await ShowErrorDialog("请输入函数表达式");
                return;
            }

            // 更新状态文本和按钮状态
            StatusText.Text = "正在计算...";
            DrawButton.IsEnabled = false;

            try
            {
                // 检查是否启用GPU计算（根据ComboBox选择）
                bool useGPU = ComputeMode.SelectedIndex == 1;
                // 异步生成函数点集
                var points = await GenerateFunctionPoints(functionExpression, useGPU);
                if (points.Count == 0)
                {
                    // 点集为空时，显示弹窗提示
                    await ShowErrorDialog("无法生成有效数据点");
                    return;
                }
                // 将新生成的点集添加到列表中
                allGraphs.Add(points);
                // 绘制所有函数图像
                DrawAllGraphs();
                // 更新状态文本，显示计算方式
                StatusText.Text = $"绘制完成 - 使用 {(useGPU ? "GPU" : "CPU")} 计算";
            }
            catch (Exception ex)
            {
                // 捕获所有未处理的异常，并通过弹窗显示错误信息
                await ShowErrorDialog($"绘制失败: {ex.Message}");
            }
            finally
            {
                // 无论成功与否，都重新启用绘制按钮
                DrawButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 处理“清除”按钮点击事件。
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // 清空画布上的所有元素
            GraphCanvas.Children.Clear();
            // 清空存储的函数点集
            allGraphs.Clear();
            // 更新状态文本
            StatusText.Text = "已清除画布";
        }

        /// <summary>
        /// 处理画布尺寸改变事件，更新尺寸并重绘。
        /// </summary>
        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 更新画布尺寸
            canvasWidth = e.NewSize.Width;
            canvasHeight = e.NewSize.Height;
            // 如果已有图像，则重新绘制以适应新尺寸
            if (allGraphs.Count > 0)
            {
                DrawAllGraphs();
            }
        }

        /// <summary>
        /// 异步生成函数点集。
        /// </summary>
        /// <param name="expression">函数表达式</param>
        /// <param name="useGPU">是否使用GPU计算</param>
        /// <returns>函数点集</returns>
        private async System.Threading.Tasks.Task<List<Point>> GenerateFunctionPoints(string expression, bool useGPU)
        {
            // 在后台线程执行计算任务
            return await System.Threading.Tasks.Task.Run(() =>
            {
                List<Point> points = new List<Point>();
                // 计算x轴步长
                double step = (xAxisMax - xAxisMin) / 2000;
                // 根据选择调用CPU或GPU方法生成点集
                // 注意：当前GPU方法直接调用了CPU方法，实际GPU加速需要额外实现
                if (useGPU)
                {
                    points = GeneratePointsWithGPU(expression, step);
                }
                else
                {
                    points = GeneratePointsWithCPU(expression, step);
                }
                return points;
            });
        }

        /// <summary>
        /// 使用CPU计算生成函数点集。
        /// </summary>
        private List<Point> GeneratePointsWithCPU(string expression, double step)
        {
            List<Point> points = new List<Point>();
            // 遍历x轴范围，按步长计算y值
            for (double x = xAxisMin; x <= xAxisMax; x += step)
            {
                try
                {
                    // 计算函数在x处的值
                    double y = EvaluateExpression(expression, x);
                    // 检查y值是否有效且在合理范围内
                    if (!double.IsNaN(y) && !double.IsInfinity(y) && Math.Abs(y) < 1e6)
                    {
                        points.Add(new Point(x, y));
                    }
                }
                catch
                {
                    // 忽略单个点计算中的错误，继续下一个点
                    continue;
                }
            }
            return points;
        }

        /// <summary>
        /// 使用GPU计算生成函数点集（当前实现为占位符，直接调用CPU方法）。
        /// </summary>
        private List<Point> GeneratePointsWithGPU(string expression, double step)
        {
            // TODO: 实现真正的GPU加速计算逻辑
            return GeneratePointsWithCPU(expression, step);
        }

        /// <summary>
        /// 计算数学表达式的值。
        /// </summary>
        private double EvaluateExpression(string expression, double x)
        {
            // 预处理表达式字符串
            expression = expression.Replace(" ", "").ToLower();
            // 将表达式中的 'x' 替换为具体的数值
            expression = Regex.Replace(expression, @"\bx\b", x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // 替换数学函数名称为 .NET Math 类方法
            expression = expression.Replace("sin", "Math.Sin");
            expression = expression.Replace("cos", "Math.Cos");
            expression = expression.Replace("tan", "Math.Tan");
            expression = expression.Replace("log", "Math.Log10"); // 常用对数
            expression = expression.Replace("ln", "Math.Log");    // 自然对数
            expression = expression.Replace("sqrt", "Math.Sqrt");
            expression = expression.Replace("abs", "Math.Abs");
            expression = expression.Replace("exp", "Math.Exp");
            // 替换数学常数
            expression = expression.Replace("pi", Math.PI.ToString(System.Globalization.CultureInfo.InvariantCulture));
            expression = expression.Replace("e", Math.E.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // 处理隐式乘法，例如 "2x" -> "2*x"
            expression = Regex.Replace(expression, @"(\d)([a-zA-Z])", "$1*$2");
            expression = Regex.Replace(expression, @"([a-zA-Z])(\d)", "$1*$2");
            // 处理函数与括号间的隐式乘法，例如 "x(x+1)" -> "x*(x+1)"
            expression = Regex.Replace(expression, @"(\w)\(", "$1*(");

            // 使用 DataTable.Compute 来计算表达式（注意：这存在安全风险，仅适用于受信任的输入）
            var dataTable = new System.Data.DataTable();
            var result = dataTable.Compute(expression, null);
            return Convert.ToDouble(result);
        }

        /// <summary>
        /// 绘制所有存储的函数图像。
        /// </summary>
        private void DrawAllGraphs()
        {
            // 清空画布
            GraphCanvas.Children.Clear();
            if (allGraphs.Count > 0)
            {
                // 绘制坐标轴
                DrawAxes();
                // 遍历每个函数的点集并绘制
                for (int graphIndex = 0; graphIndex < allGraphs.Count; graphIndex++)
                {
                    var points = allGraphs[graphIndex];
                    if (points.Count > 1)
                    {
                        // 使用 Path 和 PathGeometry 来高效绘制连续线条
                        PathFigure pathFigure = new PathFigure();
                        // 设置路径起点
                        pathFigure.StartPoint = ConvertToPoint(points[0]);
                        // 添加后续点作为线段
                        for (int i = 1; i < points.Count; i++)
                        {
                            LineSegment lineSegment = new LineSegment();
                            lineSegment.Point = ConvertToPoint(points[i]);
                            pathFigure.Segments.Add(lineSegment);
                        }
                        PathGeometry pathGeometry = new PathGeometry();
                        pathGeometry.Figures.Add(pathFigure);
                        Microsoft.UI.Xaml.Shapes.Path path = new Microsoft.UI.Xaml.Shapes.Path();
                        path.Data = pathGeometry;
                        // 设置线条颜色和粗细
                        path.Stroke = new SolidColorBrush(Colors.Blue);
                        path.StrokeThickness = 2;
                        // 将路径添加到画布
                        GraphCanvas.Children.Add(path);
                    }
                }
            }
            else
            {
                // 如果没有图像，显示状态文本
                StatusText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 将数学坐标系中的点转换为画布坐标系中的点。
        /// </summary>
        private Windows.Foundation.Point ConvertToPoint(Windows.Foundation.Point mathPoint)
        {
            // X坐标转换：线性映射到画布宽度
            double x = (mathPoint.X - xAxisMin) / (xAxisMax - xAxisMin) * canvasWidth;
            // Y坐标转换：线性映射到画布高度，并进行翻转（因为画布Y轴向下）
            double y = canvasHeight - ((mathPoint.Y - yAxisMin) / (yAxisMax - yAxisMin) * canvasHeight);
            return new Windows.Foundation.Point(x, y);
        }

        /// <summary>
        /// 绘制坐标轴。
        /// </summary>
        private void DrawAxes()
        {
            // 计算X轴在画布上的Y坐标
            double xAxisY = canvasHeight - ((0 - yAxisMin) / (yAxisMax - yAxisMin) * canvasHeight);
            // 计算Y轴在画布上的X坐标
            double yAxisX = (0 - xAxisMin) / (xAxisMax - xAxisMin) * canvasWidth;

            // 如果X轴在画布可视范围内，则绘制X轴
            if (xAxisY >= 0 && xAxisY <= canvasHeight)
            {
                Line xAxis = new Line();
                xAxis.X1 = 0;
                xAxis.Y1 = xAxisY;
                xAxis.X2 = canvasWidth;
                xAxis.Y2 = xAxisY;
                xAxis.Stroke = new SolidColorBrush(Colors.Black);
                xAxis.StrokeThickness = 1;
                GraphCanvas.Children.Add(xAxis);
            }
            // 如果Y轴在画布可视范围内，则绘制Y轴
            if (yAxisX >= 0 && yAxisX <= canvasWidth)
            {
                Line yAxis = new Line();
                yAxis.X1 = yAxisX;
                yAxis.Y1 = 0;
                yAxis.X2 = yAxisX;
                yAxis.Y2 = canvasHeight;
                yAxis.Stroke = new SolidColorBrush(Colors.Black);
                yAxis.StrokeThickness = 1;
                GraphCanvas.Children.Add(yAxis);
            }
        }

        /// <summary>
        /// 显示错误信息的弹窗。
        /// </summary>
        /// <param name="message">要显示的错误信息</param>
        private async System.Threading.Tasks.Task ShowErrorDialog(string message)
        {
            // 创建 ContentDialog 实例
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "错误", // 弹窗标题
                Content = message, // 弹窗内容
                CloseButtonText = "确定", // 关闭按钮文本
                XamlRoot = this.Content.XamlRoot // 必须设置，否则在 WinUI 3 中会出错
            };
            // 弹出并等待用户关闭对话框
            await errorDialog.ShowAsync();
        }

    }
}