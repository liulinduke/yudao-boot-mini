from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.lib.colors import HexColor
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.utils import ImageReader
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.lib.units import mm
import os

OUT = r'D:\Work\yudao-boot-mini\output\pdf\易洋出海-Facebook社媒AI获客平台介绍.pdf'
IMG = r'C:\Users\10378\Desktop\功能\官网'
os.makedirs(os.path.dirname(OUT), exist_ok=True)

FONT = r'C:\Windows\Fonts\msyh.ttc'
FONT_B = r'C:\Windows\Fonts\msyhbd.ttc'
pdfmetrics.registerFont(TTFont('YaHei', FONT))
pdfmetrics.registerFont(TTFont('YaHei-Bold', FONT_B if os.path.exists(FONT_B) else FONT))

W, H = A4
NAVY = HexColor('#102033')
BLUE = HexColor('#2F6BEA')
CYAN = HexColor('#18BBD2')
MUTED = HexColor('#64748B')
LIGHT = HexColor('#F4F7FB')
LINE = HexColor('#DCE5F1')
ORANGE = HexColor('#F59E5B')

def para(c, text, x, y, w, h, size=11, color=NAVY, leading=None, bold=False, align=TA_LEFT):
    st = ParagraphStyle('p', fontName='YaHei-Bold' if bold else 'YaHei', fontSize=size,
                        leading=leading or size*1.55, textColor=color, alignment=align,
                        spaceAfter=0, spaceBefore=0)
    p = Paragraph(text, st)
    p.wrapOn(c, w, h)
    p.drawOn(c, x, y+h-p.height)
    return p.height

def img_fit(c, path, x, y, w, h, crop=False):
    from PIL import Image
    im = Image.open(path)
    iw, ih = im.size
    if crop:
        scale = max(w/iw, h/ih)
    else:
        scale = min(w/iw, h/ih)
    dw, dh = iw*scale, ih*scale
    if crop:
        c.saveState(); c.rect(x,y,w,h,stroke=0,fill=0); c.clipPath(c.beginPath(), stroke=0, fill=0)
        # use drawImage with preserveAspectRatio and anchor; clipping via simple image placement
    dx, dy = x+(w-dw)/2, y+(h-dh)/2
    c.drawImage(ImageReader(im), dx, dy, dw, dh, mask='auto', preserveAspectRatio=True)
    if crop: c.restoreState()

def header(c, page, title='易洋出海 | Facebook 社媒 AI 获客平台'):
    c.setFillColor(NAVY); c.rect(0,H-17*mm,W,17*mm,fill=1,stroke=0)
    c.setFillColor(BLUE); c.roundRect(16*mm,H-13*mm,8*mm,8*mm,2*mm,fill=1,stroke=0)
    para(c,'Y',17.9*mm,H-12.2*mm,4*mm,5*mm,10,colors.white,bold=True,align=TA_CENTER)
    para(c,title,28*mm,H-12.5*mm,100*mm,6*mm,9,colors.white,bold=True)
    para(c,f'{page:02d}',W-27*mm,H-12.5*mm,12*mm,6*mm,9,HexColor('#B8C6DC'),align=TA_CENTER)

def footer(c):
    c.setStrokeColor(LINE); c.line(18*mm,12*mm,W-18*mm,12*mm)
    para(c,'了解更多：eyoch.com',18*mm,5*mm,70*mm,5*mm,8,MUTED)
    para(c,'矩阵账号 · 指纹浏览器 · AI Agent',W-95*mm,5*mm,77*mm,5*mm,8,MUTED,align=TA_CENTER)

def bullet(c, n, title, body, x, y, w, color=BLUE):
    c.setFillColor(color); c.circle(x+4*mm,y+4*mm,4*mm,fill=1,stroke=0)
    para(c,str(n),x+1.5*mm,y+1.7*mm,5*mm,5*mm,9,colors.white,bold=True,align=TA_CENTER)
    para(c,title,x+12*mm,y+4*mm,w-12*mm,7*mm,12,NAVY,bold=True)
    para(c,body,x+12*mm,y-11*mm,w-12*mm,16*mm,9,MUTED,leading=14)

c = canvas.Canvas(OUT, pagesize=A4)
c.setTitle('易洋出海 - Facebook 社媒 AI 获客平台介绍')

# page 1 cover
c.setFillColor(colors.white); c.rect(0,0,W,H,fill=1,stroke=0)
img_fit(c, os.path.join(IMG,'1.png'), 0, H-113*mm, W, 104*mm)
c.setFillColor(BLUE); c.rect(0,0,W,103*mm,fill=1,stroke=0)
para(c,'易洋出海',20*mm,83*mm,70*mm,10*mm,18,colors.white,bold=True)
para(c,'Facebook 获客，像一条\n自动运转的生产线',20*mm,49*mm,165*mm,31*mm,25,colors.white,leading=31,bold=True)
para(c,'面向外贸企业的社媒 AI 获客与运营平台',20*mm,38*mm,150*mm,9*mm,12,HexColor('#DCE8FF'))
c.setFillColor(ORANGE); c.roundRect(20*mm,20*mm,44*mm,10*mm,5*mm,fill=1,stroke=0)
para(c,'矩阵账号 × AI Agent',20*mm,22.7*mm,44*mm,5*mm,9,colors.white,bold=True,align=TA_CENTER)
para(c,'让每一个 Facebook 账号，都成为持续工作的获客入口',72*mm,22.5*mm,110*mm,6*mm,9,HexColor('#E6EEFF'))
c.showPage()

# page 2 positioning
header(c,2); footer(c)
para(c,'把“找客户”从人工搜索，变成可持续的系统流程',18*mm,H-37*mm,174*mm,16*mm,21,NAVY,bold=True)
para(c,'Facebook 上有大量公开需求，但人工找群、翻帖、筛选、跟进，往往耗时且难以稳定复制。易洋出海把账号、浏览器环境、AI 判断和运营动作放到同一套工作台中。',18*mm,H-61*mm,174*mm,20*mm,10,MUTED,leading=16)
c.setFillColor(LIGHT); c.roundRect(18*mm,H-107*mm,174*mm,36*mm,4*mm,fill=1,stroke=0)
para(c,'系统定位',26*mm,H-82*mm,30*mm,7*mm,10,BLUE,bold=True)
para(c,'矩阵账号管理  +  指纹浏览器隔离  +  AI 获客 Agent  +  运营自动化',26*mm,H-99*mm,157*mm,13*mm,15,NAVY,bold=True)
para(c,'适用于外贸工厂、贸易公司、品牌出海团队和需要持续开发客户的销售团队。',26*mm,H-111*mm,157*mm,8*mm,9,MUTED)
bullet(c,1,'账号与环境统一管理','账号导入、分组、代理和指纹浏览器环境集中维护，方便按账号分配任务。',18*mm,H-151*mm,174*mm)
bullet(c,2,'从公开内容中发现需求','围绕主页、帖子、群组、评论和竞品等公开来源，持续采集可分析的信息。',18*mm,H-187*mm,174*mm,CYAN)
bullet(c,3,'AI 识别真正值得跟进的线索','结合产品、行业和上下文判断买家意向，区分采购需求、同行推广和无关内容。',18*mm,H-223*mm,174*mm,ORANGE)
para(c,'系统用于提升获客效率，具体执行范围和触达方式由你的配置与人工确认决定。',18*mm,18*mm,174*mm,8*mm,8,MUTED,align=TA_CENTER)
c.showPage()

# page 3 workflow
header(c,3); footer(c)
para(c,'AI 获客 Agent：一次配置，持续发现、判断和推进',18*mm,H-37*mm,174*mm,14*mm,21,NAVY,bold=True)
para(c,'把目标客户、产品信息和触达规则配置好，Agent 按流程执行，业务员在系统里查看线索与记录。',18*mm,H-58*mm,174*mm,10*mm,10,MUTED)
img_fit(c,os.path.join(IMG,'13f3d833-6a58-4690-abf5-abe97420416f.png'),18*mm,83*mm,174*mm,86*mm)
bullet(c,1,'创建 Agent','配置产品、目标客户、关键词、群组或竞品来源。',18*mm,64*mm,84*mm)
bullet(c,2,'发现需求','持续读取 Facebook 公开内容，沉淀待分析对象。',108*mm,64*mm,84*mm,CYAN)
bullet(c,3,'AI 识别与筛选','判断买家/卖家与意向等级，给出匹配原因。',18*mm,34*mm,84*mm,ORANGE)
bullet(c,4,'触达与跟进','按配置生成评论或私信建议，并在记录中追踪结果。',108*mm,34*mm,84*mm,BLUE)
c.showPage()

# page 4 capabilities/CTA
header(c,4); footer(c)
para(c,'一套工作台，覆盖从发现到跟进的关键环节',18*mm,H-37*mm,174*mm,14*mm,21,NAVY,bold=True)
img_fit(c,os.path.join(IMG,'d25af46e-f7da-4ede-a9db-7ad4be332fac.png'),18*mm,102*mm,174*mm,92*mm)
c.setFillColor(LIGHT); c.roundRect(18*mm,55*mm,174*mm,37*mm,4*mm,fill=1,stroke=0)
para(c,'五类 AI 获客 Agent',26*mm,78*mm,60*mm,7*mm,11,BLUE,bold=True)
para(c,'AI 群帖获客　 AI 帖子获客　 AI 公共主页获客\nAI 群帖评论截流　 AI 竞品监控',26*mm,61*mm,155*mm,15*mm,11,NAVY,leading=17,bold=True)
para(c,'同时支持 Facebook 账号矩阵、资源库、采集任务、线索列表、触达记录、消息管理，以及私信、评论、发帖、转帖和加组等运营工具。',18*mm,38*mm,174*mm,13*mm,9,MUTED,leading=14)
c.setFillColor(BLUE); c.roundRect(18*mm,20*mm,174*mm,14*mm,7*mm,fill=1,stroke=0)
para(c,'想看看你的产品适合怎样在 Facebook 上找客户？  访问 eyoch.com',22*mm,23.7*mm,166*mm,7*mm,10,colors.white,bold=True,align=TA_CENTER)
c.showPage()
c.save()
print(OUT)
