# Graph Based Neural Network Module

### Related Article:
https://dergipark.org.tr/tr/pub/jarnas/issue/60593/890552

### İlgili Tez:
https://tez.yok.gov.tr/UlusalTezMerkezi/tezDetay.jsp?id=LVwBX_SlCM4HtgdYZAQG2w&no=OKF_mNC5C2z2Kckp986CYQ

### Projeyi Çalıştırmak İçin

Proje 4 bileşen içermektedir. Bunlar neo4j, backend, frontend, sql bileşenleridir. Projenin düzgün çalışabilmesi için bu 4 bileşenin de çalışması gereklidir.

#### Neo4j
Neo4j Desktop indirilip kurulması gerekmektedir. Neo4j desktop üzerinde bir "Project" açılmalı, projenin içinde "New Graph" -> "Create a Local Graph" denilerek yeni bir graph oluşturulmalıdır. Lokalde denenen örnekte Models ismiyle bir graph açılmıştır. Graph oluşturulduktan sonra "Start" butonuyla çalıştırılmalıdır. 
Çalışan neo4j sunucusunun bilgileri proje içinde aşağıdaki sınıflarda güncellenmelidir.
Startup.cs içerisinde 
```
services.AddSingleton(GraphDatabase.Driver("bolt://localhost:11002", AuthTokens.Basic("neo4j", "password")));
```
Neo4jHelper.cs içerisinde 
```
var client = new GraphClient(new Uri("http://localhost:11001/db/data"), "neo4j", "password");
```
home.component.ts içerisinde
```
var config = {
      container_id: "viz",
      server_url: "bolt://localhost:11002",
      server_user: "neo4j",
      server_password: "password",
```
#### Sql Server
Kullanıcı authentication ve authorizationu için ms sql server'ın bilgisayarda oluşturulması gereklidir. Microsoft sql server kurulup çalıştırılmalıdır. Sql serverın connection stringi appsettings.json dosyasından güncellenmelidir. 
```
"ConnectionStrings": {
    "DefaultConnection": "Server=DESKTOP-218DN59;Database=GraphBasedNeuralNetwork;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
```
#### Önyüz sunucusu
Eğer javascript node paketleri kurulmamışsa proje dizininde terminalde
```
npm i
```
ile node paketleri kurulur.

Backend projesi build edilir ve iis sunucuda çalıştırılır. 

```
npm start
```
ile angular önyüz/frontend uygulaması çalıştırılır.

Angular uygulaması webpack.config.js dosyasındaki "apiUrl: 'http://localhost:50091'" e istek atacak şekilde çalışmaktadır. Backend/IIS api farklı bir routeta çalışıyor ise güncellenmesi gerekir.

### Kullanıma Hazır YSA Modelleri

Projede H5 model import etmek için ve cnn model import etmek için hazır modeller bulunmaktadır. Bu modeller "Content/H5Files" dizininde bulunmaktadır. H5 modellerinin json formatına çevrilmesini sağlayacak python scriptleri de bu klasörde bulunmaktadır.
