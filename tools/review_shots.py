from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
import argparse
p=argparse.ArgumentParser()
p.add_argument('folder')
p.add_argument('--select',default='')
a=p.parse_args()
root=Path(a.folder)
wanted=set(filter(None,a.select.split(',')))
files=[f for f in sorted(root.glob('*.png')) if not wanted or f.stem in wanted]
for f in files:
    print(f.name, f.stat().st_size)
thumbs=[]
for f in files:
    im=Image.open(f).convert('RGB')
    im.thumbnail((600,340))
    panel=Image.new('RGB',(620,390),'#222222')
    panel.paste(im,((620-im.width)//2,32))
    ImageDraw.Draw(panel).text((10,10),f.stem,fill='white')
    thumbs.append(panel)
cols=3
rows=(len(thumbs)+cols-1)//cols
sheet=Image.new('RGB',(cols*620,rows*390),'#111111')
for i,im in enumerate(thumbs):
    sheet.paste(im,((i%cols)*620,(i//cols)*390))
out=root/'review_sheet.jpg'
sheet.save(out,quality=86)
print('SHEET',out,out.stat().st_size)
