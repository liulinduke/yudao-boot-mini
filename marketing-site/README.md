# 易洋出海 SocialMatrix 官网

这是易洋出海 SocialMatrix 的生产官网页面，不依赖后台，入口是 `index.html`。

## 本地预览

可以直接双击 `index.html`，也可以在该目录执行：

```bash
npx serve .
```

然后打开命令行显示的本地地址。

## 上线前替换

1. 将 `downloads/yiyang-socialmatrix-product-placeholder.txt` 替换成真实 Windows 客户端安装包。
2. 如联系方式发生变化，在 `app.js` 顶部的 `contactConfig` 中更新手机号和地址。
3. 当前文案只宣传已开发的 Facebook 能力，没有加入虚构的客户数量、认证或案例数据。
