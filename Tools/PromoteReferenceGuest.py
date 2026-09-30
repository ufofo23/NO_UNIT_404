"""Copy validated character assets back from the isolated Unity project."""
import hashlib, pathlib, shutil, xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[1]
VALIDATION=ROOT/'Builds/ReferenceGuestValidationProject'
FOLDER=pathlib.Path('Assets/_Project/Resources/NO404/Characters/FirstGuest')
result=ET.parse(ROOT/'Logs/ReferenceGuestPlayMode.xml').getroot()
assert result.get('result')=='Passed' and result.get('failed')=='0', 'PlayMode validation must pass first.'
for name in ['FirstGuest.fbx','Worker_BaseColor.png']:
    a=(ROOT/FOLDER/name).read_bytes();b=(VALIDATION/FOLDER/name).read_bytes()
    assert hashlib.sha256(a).digest()==hashlib.sha256(b).digest(), 'Validated source differs: '+name
for name in ['FirstGuestCharacter.prefab','FirstGuestCharacter.prefab.meta','FirstGuest.fbx.meta',
             'Worker_BaseColor.png.meta','ReferenceWorker.mat','ReferenceWorker.mat.meta']:
    shutil.copy2(VALIDATION/FOLDER/name,ROOT/FOLDER/name)
    print('Applied',name)
for name in ['FirstGuest_Interphone.png','FirstGuest_InGame.png']:
    source=VALIDATION/'Docs/Art/FirstGuest'/name
    shutil.copy2(source,ROOT/'Docs/Art/FirstGuest'/name)
    shutil.copy2(source,ROOT/'Docs/Art/ReferenceGuest'/name.replace('FirstGuest','ReferenceGuest'))
print('Validated character and game screenshots applied.')
