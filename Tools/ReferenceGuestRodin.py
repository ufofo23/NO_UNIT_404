"""Use the installed BlenderMCP public trial integration, never a private paid key.
The four crops are input preparation for multi-view 3D reconstruction, not edited art.
Generation is an explicit one-shot operation; status/download do not create new jobs.
"""
import ast, io, json, pathlib, sys
import requests
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'Docs/Art/ReferenceGuest'
OUT.mkdir(parents=True, exist_ok=True)
ADDON = pathlib.Path.home() / 'AppData/Roaming/Blender Foundation/Blender/5.2/scripts/addons/blender_mcp.py'
tree = ast.parse(ADDON.read_text(encoding='utf-8'))
key = next(ast.literal_eval(n.value) for n in tree.body if isinstance(n, ast.Assign)
           and any(isinstance(t, ast.Name) and t.id == 'RODIN_FREE_TRIAL_KEY' for t in n.targets))
headers = {'Authorization': 'Bearer ' + key}
base = 'https://hyperhuman.deemos.com/api/v2/'
action = sys.argv[1]
job_path = OUT / 'rodin_job.json'

if action == 'generate':
    if job_path.exists():
        raise SystemExit('A job is already recorded. Poll it instead of submitting twice.')
    reference = ROOT / 'NO_UNIT_404_ADULT_CHARACTER_REFERENCES/ChatGPT Image 2026년 9월 8일 오후 06_52_15.png'
    image = Image.open(reference).convert('RGB')
    print('Reference dimensions:', image.size)
    regions = [('Front',(28,20,442,1009)), ('Back',(706,20,1098,1009)),
               ('Left',(457,20,690,1009)), ('Right',(1118,20,1388,1009))]
    files = []
    for name, box in regions:
        crop = image.crop(box)
        target = OUT / (name + '_input.png')
        crop.save(target)
        files.append(('images',(name+'.png', target.read_bytes(), 'image/png')))
    files.extend((name,(None,value)) for name,value in {
        'tier':'Sketch', 'mesh_mode':'Raw', 'texture_mode':'high',
        'prompt':'One single realistic adult Korean male maintenance worker, all input images are different views of the SAME man. Match the face, body proportions and exact clothing from the reference. Black baseball cap, brown work shirt with long sleeves, lime yellow mesh reflective safety vest with silver reflective strips, charcoal cargo trousers, red and black work gloves, black work shoes. Standing relaxed A pose, separate fingers. Full body, physically realistic materials, photorealistic human anatomy. No base, no ground, no text, no extra people.',
        'bbox_condition':'[7, 18, 4]'
    }.items())
    response = requests.post(base+'rodin', headers=headers, files=files, timeout=120)
    data = response.json()
    print('HTTP',response.status_code)
    if response.ok and 'uuid' in data:
        job_path.write_text(json.dumps(data,indent=2),encoding='utf-8')
        print('Job recorded; response fields:',list(data))
    else:
        print(json.dumps(data,ensure_ascii=False)[:1500])
        raise SystemExit(1)
elif action == 'status':
    job = json.loads(job_path.read_text(encoding='utf-8'))
    subscription = job.get('jobs',{}).get('subscription_key') or job.get('subscription_key')
    response = requests.post(base+'status',headers=headers,json={'subscription_key':subscription},timeout=40)
    data = response.json()
    (OUT/'rodin_status.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
    print('HTTP',response.status_code,'statuses',[(j.get('status'),j.get('name')) for j in data.get('jobs',[])])
    if not response.ok: print(json.dumps(data)[:1000])
elif action == 'download':
    job = json.loads(job_path.read_text(encoding='utf-8'))
    response=requests.post(base+'download',headers=headers,json={'task_uuid':job['uuid']},timeout=40)
    response.raise_for_status()
    data=response.json()
    (OUT/'rodin_download.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
    for item in data.get('list',[]):
        name=pathlib.Path(item['name']).name
        result=requests.get(item['url'],timeout=120);result.raise_for_status()
        (OUT/name).write_bytes(result.content)
        print('Downloaded',name,len(result.content))
