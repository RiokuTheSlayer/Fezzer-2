﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FezEngine.Structure;
using System.Threading;
using FmbLib;

public class LevelManager : Singleton<LevelManager> {

    Level loaded;

    public string levelName, resourcePath;
    string setName;

    [SerializeField]
    bool manualLoad;

    public GameObject trilePrefab, aoPrefab, planePrefab;

    Dictionary<TrileEmplacement, GameObject> trileObjects = new Dictionary<TrileEmplacement, GameObject>();

    //Caching
    List<GameObject> tileCache = new List<GameObject>();
    List<GameObject> aoObjectCache = new List<GameObject>();

    Dictionary<Trile, Mesh> trilesetCache = new Dictionary<Trile, Mesh>();
    Dictionary<ArtObject, Mesh> aoMeshCache = new Dictionary<ArtObject,Mesh>();
    Dictionary<int, ArtObject> aoCache = new Dictionary<int, ArtObject>();

    [HideInInspector]
    public static TrileSet s;

    void Awake() {
        OutputPath.setPath=resourcePath+"out/";

    }

    int currTrileID;

    public void PickTrile(GameObject toPick) {
        currTrileID=int.Parse(toPick.name);
    }

    public void LoadLevel() {
        LoadLevel(levelName);
    }

    public void LoadLevel(string name) {

        loaded=FmbUtil.ReadObject<Level>(OutputPath.OutputPathDir+"levels/"+name.ToLower()+".xnb");

        //Load the trile set 
        s=FmbUtil.ReadObject<TrileSet>(OutputPath.OutputPathDir+"trile sets/"+loaded.TrileSetName.ToLower()+".xnb");

        LoadUsedArtObjects();

        //Create meshes for triles
        LoadSetMeshes();
        LoadAOMeshes();

        StartCoroutine(LoadLevelCoroutine());
        ListTrilesUnderUI();
    }

    public void LoadSetMeshes() {
        setMat=new Material(Shader.Find("Standard"));
        s.TextureAtlas.filterMode=FilterMode.Point;
        setMat.mainTexture=s.TextureAtlas;
        foreach(Trile t in s.Triles.Values) {
            trilesetCache.Add(t,FezToUnity.TrileToMesh(t));
        }
    }

    public void LoadUsedArtObjects() {
        foreach (KeyValuePair<int, ArtObjectInstance> ao in loaded.ArtObjects) {

            string path = OutputPath.OutputPathDir+"art objects/"+ao.Value.ArtObjectName.ToLower()+".xnb";

            ArtObject aoL = FmbUtil.ReadObject<ArtObject>(path);
            aoCache.Add(ao.Key,aoL);
        }
    }

    public void LoadAOMeshes() {
        foreach(ArtObjectInstance ao in loaded.ArtObjects.Values) {
            ArtObject aoL = aoCache[ao.Id];
            aoMeshCache.Add(aoL,FezToUnity.ArtObjectToMesh(aoL));
        }
    }

    Dictionary<TrileEmplacement, bool> visibility = new Dictionary<TrileEmplacement, bool>();

    Material setMat;

    IEnumerator LoadLevelCoroutine() {

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        yield return new WaitForEndOfFrame();

        //Calculate level visibility
        foreach (KeyValuePair<TrileEmplacement, TrileInstance> kvp in loaded.Triles) {

            TrileEmplacement currP = kvp.Key;

            if (kvp.Value.TrileId<0) {
                visibility.Add(currP, false);
                continue;
            }

            if (kvp.Value.ForceSeeThrough||s.Triles[kvp.Value.TrileId].SeeThrough) {
                visibility.Add(currP,true);
                continue;
            }

            List<TrileEmplacement> checkPos = new List<TrileEmplacement>();

            checkPos.Add(new TrileEmplacement(currP.X, currP.Y, currP.Z+1));
            checkPos.Add(new TrileEmplacement(currP.X, currP.Y, currP.Z-1));

            checkPos.Add(new TrileEmplacement(currP.X, currP.Y+1, currP.Z));
            checkPos.Add(new TrileEmplacement(currP.X, currP.Y-1, currP.Z));

            checkPos.Add(new TrileEmplacement(currP.X+1, currP.Y, currP.Z));
            checkPos.Add(new TrileEmplacement(currP.X-1, currP.Y, currP.Z));

            bool isVisible=false;

            foreach (TrileEmplacement pos in checkPos) {

                if (!loaded.Triles.ContainsKey(pos)) {
                    visibility.Add(currP, true);
                    isVisible=true;
                    break;
                } else if (loaded.Triles[pos].TrileId<0) {
                    break;
                } else if (loaded.Triles[pos].ForceSeeThrough||s.Triles[loaded.Triles[pos].TrileId].SeeThrough) {
                    visibility.Add(currP, true);
                    isVisible=true;
                    break;
                }
            }
            if (!isVisible) {
                visibility.Add(currP,false);
            }

        }

        int index = 0;

        //Generate triles
        {

            foreach (KeyValuePair<TrileEmplacement, TrileInstance> kvp in loaded.Triles) {
                if (!visibility[kvp.Key])
                    continue;

                GameObject newTrile = Instantiate(trilePrefab);

                MeshFilter mf = newTrile.GetComponent<MeshFilter>();
                MeshRenderer mr = newTrile.GetComponent<MeshRenderer>();
                BoxCollider bc = newTrile.GetComponent<BoxCollider>();

                mr.material=setMat;
                mf.mesh=trilesetCache[s.Triles[kvp.Value.TrileId]];

                bc.size=mf.mesh.bounds.size;
                bc.center=mf.mesh.bounds.center;

                newTrile.transform.position=new Vector3(kvp.Key.X,kvp.Key.Y,kvp.Key.Z);
                newTrile.transform.rotation=Quaternion.Euler(0,Mathf.Rad2Deg*kvp.Value.Data.PositionPhi.w,0);

                index++;

                if (index>50) {
                    index=0;
                    //yield return new WaitForEndOfFrame();
                }

                newTrile.name=s.Triles[kvp.Value.TrileId].Name;
                newTrile.transform.parent=transform.FindChild("Triles");
                trileObjects.Add(kvp.Key,newTrile);
            }

            //Generate Planes
            {
                foreach(KeyValuePair<int, BackgroundPlane> kvp in loaded.BackgroundPlanes) {

                    BackgroundPlane b = kvp.Value;

                    if (b.Hidden)
                        continue;

                    GameObject newPlane = Instantiate(planePrefab);

                    newPlane.transform.rotation=b.Rotation;
                    newPlane.transform.position=b.Position-(Vector3.one/2);
                    newPlane.transform.localScale=new Vector3(-b.Size.x,-b.Size.y,b.Size.z);

                    MeshRenderer mr = newPlane.GetComponent<MeshRenderer>();

                    try {
                        Texture2D tex = FmbUtil.ReadObject<Texture2D>(OutputPath.OutputPathDir+"background planes/"+b.TextureName.ToLower()+".xnb");

                        if (tex!=null) {
                            tex.alphaIsTransparency=true;
                            mr.material.mainTexture=tex;
                            mr.material.mainTexture.filterMode=FilterMode.Point;
                        } else
                            Debug.Log("Tex Null!");
                    } catch(System.Exception e) {
                        Debug.Log(e);
                        Debug.Log(b.TextureName.ToLower());
                        Destroy(newPlane);
                        continue;
                    }

                    newPlane.name=b.TextureName;

                    index++;

                    if (index>5) {
                        index=0;
                        //yield return new WaitForEndOfFrame();
                    }

                    newPlane.transform.rotation=Quaternion.Euler(newPlane.transform.eulerAngles.x,newPlane.transform.eulerAngles.y-180,newPlane.transform.eulerAngles.z);
                    newPlane.transform.parent=transform.FindChild("ArtObjects");
                }

            }

            //Generate Art Objects
            {

                foreach (KeyValuePair<int, ArtObjectInstance> kvp in loaded.ArtObjects) {

                    GameObject newTrile = Instantiate(aoPrefab);

                    MeshFilter mf = newTrile.GetComponent<MeshFilter>();
                    MeshRenderer mr = newTrile.GetComponent<MeshRenderer>();

                    mr.material=FezToUnity.GeometryToMaterial(aoCache[kvp.Key].Cubemap);
                    mf.mesh=aoMeshCache[aoCache[kvp.Key]];

                    newTrile.transform.position=kvp.Value.Position-(Vector3.one/2);
                    newTrile.transform.rotation=kvp.Value.Rotation;

                    index++;

                    if (index>5) {
                        index=0;
                        //yield return new WaitForEndOfFrame();
                    }
                    newTrile.name=kvp.Value.ArtObjectName;
                    newTrile.transform.parent=transform.FindChild("ArtObjects");
                }
            }
        }

        sw.Stop();
        Debug.Log(sw.ElapsedMilliseconds);
    }

    public void RemoveTrile(TrileEmplacement trilePos) {
        if (!loaded.Triles.ContainsKey(trilePos))
            return;
        loaded.Triles.Remove(trilePos);
        
        Destroy(trileObjects[trilePos]);
        trileObjects.Remove(trilePos);
    }

    public void AddTrile(TrileEmplacement trilePos) {
        if (loaded.Triles.ContainsKey(trilePos))
            return;

        TrileInstance newInstance = new TrileInstance();
        newInstance.TrileId=currTrileID;
        newInstance.Position=new Vector3(trilePos.X,trilePos.Y,trilePos.Z);

        loaded.Triles.Add(trilePos,newInstance);

        GameObject newTrile = Instantiate(trilePrefab);

        MeshFilter mf = newTrile.GetComponent<MeshFilter>();
        MeshRenderer mr = newTrile.GetComponent<MeshRenderer>();
        BoxCollider bc = newTrile.GetComponent<BoxCollider>();

        mr.material=setMat;
        mf.mesh=trilesetCache[s.Triles[newInstance.TrileId]];

        bc.size=mf.mesh.bounds.size;
        bc.center=mf.mesh.bounds.center;

        newTrile.transform.position=new Vector3(trilePos.X, trilePos.Y, trilePos.Z);
        newTrile.transform.rotation=Quaternion.Euler(0, 0, 0);

        newTrile.name=s.Triles[newInstance.TrileId].Name;
        newTrile.transform.parent=transform.FindChild("Triles");
        trileObjects.Add(trilePos, newTrile);
    }

    [SerializeField]
    RectTransform horizontal;
    [SerializeField]
    GameObject buttonPrefab;

    int columnCount = 4;

    void ListTrilesUnderUI() {

        RectTransform rowRectTransform = buttonPrefab.GetComponent<RectTransform>();
        RectTransform containerRectTransform = horizontal.GetComponent<RectTransform>();
        int itemCount = s.Triles.Count;

        //calculate the width and height of each child item.
        float width = containerRectTransform.rect.width/columnCount;
        float ratio = width/rowRectTransform.rect.width;
        float height = rowRectTransform.rect.height*ratio;

        height/=2;
        width/=2;

        int rowCount = itemCount/columnCount;
        if (itemCount%rowCount>0)
            rowCount++;

        //adjust the height of the container so that it will just barely fit all its children
        float scrollHeight = height*rowCount;
        containerRectTransform.offsetMin=new Vector2(containerRectTransform.offsetMin.x, -scrollHeight/2);
        containerRectTransform.offsetMax=new Vector2(containerRectTransform.offsetMax.x, scrollHeight/2);

        int j = 0;
        for (int i = 0; i<itemCount; i++) {
            //this is used instead of a double for loop because itemCount may not fit perfectly into the rows/columns
            if (i%columnCount==0)
                j++;

            //create a new item, name it, and set the parent
            GameObject newItem = Instantiate(buttonPrefab);
            newItem.name=gameObject.name+" item at ("+i+","+j+")";
            newItem.transform.parent=horizontal;

            //move and size the new item
            RectTransform rectTransform = newItem.GetComponent<RectTransform>();

            float x = -containerRectTransform.rect.width/2+width*(i%columnCount);
            float y = containerRectTransform.rect.height/2-height*j;
            rectTransform.offsetMin=new Vector2(x, y);

            x=rectTransform.offsetMin.x+width;
            y=rectTransform.offsetMin.y+height;
            rectTransform.offsetMax=new Vector2(x, y);
        }

    }
}
