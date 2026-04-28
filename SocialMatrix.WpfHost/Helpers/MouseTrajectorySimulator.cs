using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;

namespace SocialMatrix.WpfHost.Helpers
{
    /// <summary>
    /// 鼠标轨迹模拟器 - 使用贝塞尔曲线模拟人手移动
    /// </summary>
    public class MouseTrajectorySimulator
    {
        private readonly Random _random = new Random();

        /// <summary>
        /// 模拟人手鼠标移动到目标位置（使用贝塞尔曲线）
        /// </summary>
        /// <param name="browser">浏览器实例</param>
        /// <param name="targetX">目标X坐标</param>
        /// <param name="targetY">目标Y坐标</param>
        /// <param name="durationMs">移动持续时间（毫秒），默认800-1500ms模拟人手速度</param>
        public async Task SimulateHumanMouseMove(ChromiumWebBrowser browser, int targetX, int targetY, int? durationMs = null)
        {
            // 生成贝塞尔曲线路径点
            var trajectoryPoints = GenerateBezierTrajectory(targetX, targetY);
            
            // 确定移动总时长（模拟人手速度）
            int totalDuration = durationMs ?? _random.Next(800, 1500);
            int stepDelay = totalDuration / trajectoryPoints.Count;
            
            // 逐步移动鼠标
            foreach (var point in trajectoryPoints)
            {
                await MoveMouseTo(browser, point.X, point.Y);
                await Task.Delay(stepDelay + _random.Next(-50, 50)); // 添加随机延迟
            }
            
            // 最后确保到达目标位置
            await MoveMouseTo(browser, targetX, targetY);
        }

        /// <summary>
        /// 生成贝塞尔曲线路径点
        /// </summary>
        private List<Point> GenerateBezierTrajectory(int targetX, int targetY)
        {
            // 起点（假设从屏幕中心或当前位置开始）
            Point start = new Point(_random.Next(100, 800), _random.Next(100, 600));
            Point end = new Point(targetX, targetY);
            
            // 生成2-3个控制点，使路径更自然
            int controlPointCount = _random.Next(2, 4);
            List<Point> controlPoints = new List<Point>();
            
            for (int i = 0; i < controlPointCount; i++)
            {
                // 控制点在起点和终点之间，但有偏移
                double t = (i + 1.0) / (controlPointCount + 1);
                double offsetX = _random.Next(-100, 100); // 随机偏移
                double offsetY = _random.Next(-100, 100);
                
                int cx = (int)(start.X + (end.X - start.X) * t + offsetX);
                int cy = (int)(start.Y + (end.Y - start.Y) * t + offsetY);
                
                controlPoints.Add(new Point(cx, cy));
            }
            
            // 使用三次贝塞尔曲线生成路径点
            return GenerateCubicBezierPath(start, controlPoints, end, 20); // 20个中间点
        }

        /// <summary>
        /// 生成三次贝塞尔曲线路径
        /// </summary>
        private List<Point> GenerateCubicBezierPath(Point start, List<Point> controlPoints, Point end, int segments)
        {
            var points = new List<Point>();
            points.Add(start);
            
            // 简化：使用二次贝塞尔曲线（一个控制点）
            Point control = controlPoints.Count > 0 ? controlPoints[0] : 
                           new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            
            for (int i = 1; i <= segments; i++)
            {
                double t = (double)i / segments;
                
                // 二次贝塞尔曲线公式: B(t) = (1-t)^2*P0 + 2(1-t)t*P1 + t^2*P2
                double x = Math.Pow(1 - t, 2) * start.X + 
                          2 * (1 - t) * t * control.X + 
                          Math.Pow(t, 2) * end.X;
                
                double y = Math.Pow(1 - t, 2) * start.Y + 
                          2 * (1 - t) * t * control.Y + 
                          Math.Pow(t, 2) * end.Y;
                
                // 添加微小抖动，模拟人手不稳定性
                x += _random.Next(-2, 3);
                y += _random.Next(-2, 3);
                
                points.Add(new Point((int)x, (int)y));
            }
            
            return points;
        }

        /// <summary>
        /// 移动鼠标到指定位置（通过JavaScript）
        /// </summary>
        private async Task MoveMouseTo(ChromiumWebBrowser browser, int x, int y)
        {
            try
            {
                var js = $@"
                    (function() {{
                        // 创建或获取鼠标事件
                        const event = new MouseEvent('mousemove', {{
                            view: window,
                            bubbles: true,
                            cancelable: true,
                            clientX: {x},
                            clientY: {y},
                            screenX: {x},
                            screenY: {y}
                        }});
                        
                        // 分发事件
                        document.dispatchEvent(event);
                        
                        // 更新全局鼠标位置（如果需要）
                        window.__lastMousePosition = {{ x: {x}, y: {y} }};
                    }})();
                ";
                
                await browser.EvaluateScriptAsync(js);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 鼠标移动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 模拟点击操作（包含移动+点击）
        /// </summary>
        public async Task SimulateHumanClick(ChromiumWebBrowser browser, int targetX, int targetY)
        {
            // 先移动到目标位置
            await SimulateHumanMouseMove(browser, targetX, targetY);
            
            // 短暂停留后点击
            await Task.Delay(_random.Next(100, 300));
            
            // 执行点击
            await ClickAt(browser, targetX, targetY);
        }

        /// <summary>
        /// 在指定位置点击
        /// </summary>
        private async Task ClickAt(ChromiumWebBrowser browser, int x, int y)
        {
            try
            {
                var js = $@"
                    (function() {{
                        // 创建mousedown事件
                        const downEvent = new MouseEvent('mousedown', {{
                            view: window,
                            bubbles: true,
                            cancelable: true,
                            clientX: {x},
                            clientY: {y},
                            button: 0
                        }});
                        document.dispatchEvent(downEvent);
                        
                        // 短暂延迟
                        setTimeout(() => {{
                            // 创建mouseup事件
                            const upEvent = new MouseEvent('mouseup', {{
                                view: window,
                                bubbles: true,
                                cancelable: true,
                                clientX: {x},
                                clientY: {y},
                                button: 0
                            }});
                            document.dispatchEvent(upEvent);
                            
                            // 创建click事件
                            const clickEvent = new MouseEvent('click', {{
                                view: window,
                                bubbles: true,
                                cancelable: true,
                                clientX: {x},
                                clientY: {y},
                                button: 0
                            }});
                            document.dispatchEvent(clickEvent);
                        }}, {_random.Next(50, 150)});
                    }})();
                ";
                
                await browser.EvaluateScriptAsync(js);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 鼠标点击失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 模拟滚动（带随机停顿）
        /// </summary>
        public async Task SimulateHumanScroll(ChromiumWebBrowser browser, int scrollAmount)
        {
            try
            {
                var js = $@"
                    (function() {{
                        // 分多次小幅度滚动，模拟人手
                        const steps = {_random.Next(3, 7)};
                        const stepSize = {scrollAmount} / steps;
                        
                        for (let i = 0; i < steps; i++) {{
                            window.scrollBy({{
                                top: stepSize + {_random.Next(-10, 10)},
                                behavior: 'auto'
                            }});
                            
                            // 每次滚动之间有随机停顿
                            const delay = {_random.Next(50, 200)};
                            // 注意：这里无法在循环中使用await，所以简化处理
                        }}
                    }})();
                ";
                
                await browser.EvaluateScriptAsync(js);
                await Task.Delay(_random.Next(200, 500)); // 等待滚动完成
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 滚动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部点结构
        /// </summary>
        private struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }
            
            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
