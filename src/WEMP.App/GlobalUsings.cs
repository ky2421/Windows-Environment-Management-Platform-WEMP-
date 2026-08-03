// UseWindowsForms 引入 System.Windows.Forms 全局 using，
// 与 WPF 同名类型（Application/MessageBox）冲突，此处统一解析为 WPF 版本。
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
